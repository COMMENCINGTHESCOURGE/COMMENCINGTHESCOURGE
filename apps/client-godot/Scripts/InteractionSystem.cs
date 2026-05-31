using Godot;

/// <summary>
/// Interaction system. Attached to the Player.
/// Uses Area3D proximity detection for interactable objects.
/// Shows prompts, handles E/F key actions, manages loot windows.
/// </summary>
public partial class InteractionSystem : Node3D
{
    [Export] public Area3D DetectionZone;
    [Export] public float InteractionRange = 3.0f;

    // UI references
    private Label _promptLabel;
    private Control _lootWindow;
    private Control _inventoryPanel;
    private Control _craftingPanel;
    private Control _buildingUI;

    // Current interactable
    private Interactable _currentTarget;

    // Toggle state
    private bool _inventoryOpen = false;
    private bool _craftingOpen = false;
    private bool _buildingMode = false;

    public override void _Ready()
    {
        // Find the HUD prompt label
        var hud = GetNodeOrNull<CanvasLayer>("/root/World/UI/MainHUD");
        if (hud != null)
            _promptLabel = hud.GetNodeOrNull<Label>("InteractionPrompt");

        // Load UI panels
        _lootWindow = GetNodeOrNull<Control>("/root/World/UI/LootWindow");
        _inventoryPanel = GetNodeOrNull<Control>("/root/World/UI/InventoryPanel");
        _craftingPanel = GetNodeOrNull<Control>("/root/World/UI/CraftingPanel");
        _buildingUI = GetNodeOrNull<Control>("/root/World/UI/BuildingUI");

        // Detection zone setup
        if (DetectionZone == null)
        {
            DetectionZone = new Area3D();
            var shape = new CollisionShape3D();
            shape.Shape = new SphereShape3D { Radius = InteractionRange };
            DetectionZone.AddChild(shape);
            AddChild(DetectionZone);
        }

        DetectionZone.BodyEntered += OnBodyEntered;
        DetectionZone.BodyExited += OnBodyExited;

        var matrixClient = GetNodeOrNull<MatrixClient>("/root/MatrixClient");
        if (matrixClient != null)
        {
            matrixClient.DialogueReceived += OnMatrixDialogueReceived;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_interact")) // E key
        {
            if (_currentTarget != null)
            {
                _currentTarget.OnInteract(this);
            }
        }

        if (@event.IsActionPressed("ui_talk")) // F key
        {
            if (_currentTarget != null && _currentTarget is NPC npc)
            {
                var bubble = npc.GetNodeOrNull<Label3D>("DialogueBubble");
                if (bubble != null)
                {
                    bubble.Text = "..."; // Cognitive weight indicator
                    bubble.Visible = true;
                }
                
                var matrixClient = GetNodeOrNull<MatrixClient>("/root/MatrixClient");
                if (matrixClient != null)
                {
                    matrixClient.RequestNPCDialogue(npc.Name, "direct_conversation");
                }
                else
                {
                    if (bubble != null)
                    {
                        bubble.Text = GenerateTalkDialogue(npc);
                        var timer = GetTree().CreateTimer(4.0f);
                        timer.Timeout += () => { if (bubble != null) bubble.Visible = false; };
                    }
                }
            }
        }

        if (@event.IsActionPressed("ui_inventory")) // I or Tab
        {
            ToggleInventory();
        }

        if (@event.IsActionPressed("ui_crafting")) // C
        {
            ToggleCrafting();
        }

        if (@event.IsActionPressed("ui_build")) // B
        {
            ToggleBuildingMode();
        }
    }

    public override void _Process(double delta)
    {
        // Update prompt
        if (_currentTarget != null && IsWithinRange(_currentTarget))
        {
            string prompt = _currentTarget.GetPromptText();
            _currentTarget.GetPromptActions(out string action1, out string action2);

            if (_promptLabel != null)
                _promptLabel.Text = $"[{action1}] {prompt}" + (action2 != "" ? $"    [{action2}] ..." : "");
        }
        else
        {
            if (_promptLabel != null)
                _promptLabel.Text = "";
        }

        CheckProximityDialogue();
    }

    private float _proximityTimer = 0f;
    private void CheckProximityDialogue()
    {
        // Zone-evaluation loop for 10.0.0.102 backend
        _proximityTimer += (float)GetProcessDeltaTime();
        if (_proximityTimer >= 8.0f) // Throttled to avoid overwhelming the LLM
        {
            _proximityTimer = 0f;
            var npcs = GetTree().GetNodesInGroup("NPCs");
            foreach (Node node in npcs)
            {
                if (node is NPC npc)
                {
                    float distance = GlobalPosition.DistanceTo(npc.GlobalPosition);
                    if (distance <= 5.0f)
                    {
                        string context = distance <= 1.0f ? "intimate" : (distance <= 3.0f ? "conversation" : "calling");
                        GD.Print($"Proximity Zone [{context}]: Distance {distance:F1}m to NPC {npc.Name}");
                        
                        var matrixClient = GetNodeOrNull<MatrixClient>("/root/MatrixClient");
                        if (matrixClient != null)
                        {
                            matrixClient.RequestNPCDialogue(npc.Name, context);
                            // Break out to only request one at a time
                            break;
                        }
                    }
                }
            }
        }
    }

    private void OnMatrixDialogueReceived(string npcName, string text)
    {
        var npcs = GetTree().GetNodesInGroup("NPCs");
        foreach (Node node in npcs)
        {
            if (node is NPC npc && npc.Name == npcName)
            {
                var bubble = npc.GetNodeOrNull<Label3D>("DialogueBubble");
                if (bubble != null)
                {
                    bubble.Text = text;
                    bubble.Visible = true;
                    var timer = GetTree().CreateTimer(6.0f);
                    timer.Timeout += () => { if (bubble != null) bubble.Visible = false; };
                }
                break;
            }
        }
    }

    private bool IsWithinRange(Interactable target)
    {
        if (target is Node3D node)
            return GlobalPosition.DistanceTo(node.GlobalPosition) <= InteractionRange;
        return false;
    }

    private void OnBodyEntered(Node body)
    {
        if (body is Interactable interactable)
        {
            _currentTarget = interactable;
        }
    }

    private void OnBodyExited(Node body)
    {
        if (body == _currentTarget)
        {
            _currentTarget = null;
        }
    }

    private string GenerateTalkDialogue(NPC npc)
    {
        // Quick dialogue based on current constraints
        if (ConstraintField.Infection > 0.5f)
            return "You don't look good. Stay back.";
        if (ConstraintField.Hunger < 0.2f)
            return "You look hungry. Check the east warehouse.";
        if (ConstraintField.HordeThreat > 0.7f)
            return "You hear that? Stay close to the walls.";
        return npc.NpcRole switch
        {
            "guard" => "Keep your head down. They're active tonight.",
            "medic" => "If you're hurt, I can patch you up. Got supplies?",
            "leader" => "We need more scouts. You interested?",
            _ => "Hang in there. We'll make it through."
        };
    }

    private void ToggleInventory()
    {
        _inventoryOpen = !_inventoryOpen;
        if (_inventoryPanel != null)
        {
            _inventoryPanel.Visible = _inventoryOpen;
            Input.MouseMode = _inventoryOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        }
        // Close other panels
        if (_inventoryOpen)
        {
            if (_craftingPanel != null) _craftingPanel.Visible = false;
            _craftingOpen = false;
        }
    }

    private void ToggleCrafting()
    {
        _craftingOpen = !_craftingOpen;
        if (_craftingPanel != null)
        {
            _craftingPanel.Visible = _craftingOpen;
            Input.MouseMode = _craftingOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
        }
        if (_craftingOpen)
        {
            if (_inventoryPanel != null) _inventoryPanel.Visible = false;
            _inventoryOpen = false;
        }
    }

    private void ToggleBuildingMode()
    {
        _buildingMode = !_buildingMode;
        if (_buildingUI != null)
        {
            _buildingUI.Visible = _buildingMode;
        }
    }

    /// <summary>
    /// Open a loot container window.
    /// </summary>
    public void OpenLootWindow(string containerName)
    {
        if (_lootWindow != null)
        {
            _lootWindow.Visible = true;
            var nameLabel = _lootWindow.GetNodeOrNull<Label>("TitleBar/ContainerName");
            if (nameLabel != null)
                nameLabel.Text = containerName;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public void CloseLootWindow()
    {
        if (_lootWindow != null)
            _lootWindow.Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }
}

/// <summary>
/// Base class for interactable objects. Attach to any Node3D the player can interact with.
/// </summary>
public abstract partial class Interactable : CharacterBody3D
{
    [Export] public string InteractionLabel = "interact";
    [Export] public string PrimaryAction = "E";
    [Export] public string SecondaryAction = "";

    public abstract void OnInteract(InteractionSystem system);
    public abstract string GetPromptText();

    public void GetPromptActions(out string primary, out string secondary)
    {
        primary = PrimaryAction;
        secondary = SecondaryAction;
    }
}

/// <summary>
/// Example: a lootable container. Attach to any crate/cabinet/body mesh.
/// </summary>
public partial class LootContainer : Interactable
{
    [Export] public string ContainerName = "Rusty Locker";
    [Export] public Godot.Collections.Array<string> StartingItems;

    public override void OnInteract(InteractionSystem system)
    {
        system.OpenLootWindow(ContainerName);
    }

    public override string GetPromptText()
    {
        return $"Search {ContainerName}";
    }
}
