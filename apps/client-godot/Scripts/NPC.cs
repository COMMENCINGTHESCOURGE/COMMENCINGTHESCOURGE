using Godot;

/// <summary>
/// Simple NPC with proximity dialogue from the design inventory.
/// Reads from ConstraintField for bodily vinculums.
/// </summary>
public partial class NPC : Interactable
{
    [Export] public string NpcName = "Survivor";
    [Export] public string NpcRole = "civilian";   // guard, medic, gatherer, fighter, builder, leader
    [Export] public float WalkSpeed = 2.0f;

    private Node3D _player;
    private Label3D _dialogueBubble;
    private float _dialogueTimer = 0.0f;
    private Area3D _proximityZone;

    // Zones: Intimate (0-1.5m), Conversation (1.5-4m), Calling (4-10m), Distant (10-30m)
    private enum ProximityZone { None, Intimate, Conversation, Calling, Distant }

    // Phase 17: Biological Constraints (Micro-Checkpoints)
    private Vector3 _homeAnchor;
    private Vector3 _eatAnchor;
    private Vector3 _workAnchor;
    private Vector3 _currentAnchor;

    // Phase 22: The Hygiene Scourge
    private Vector3 _washAnchor;
    [Export] public float HygieneLevel = 100.0f;
    [Export] public float InfectionLevel = 0.0f;
    private bool _isScourge = false;

    // Phase 21: Property Preservation
    private PropertyIntegrity _repairTarget;
    // Phase 26: Hyper-Real Proxemics
    private HyperRealHead _headController;

    public override void _Ready()
    {
        _player = (Node3D)GetTree().GetFirstNodeInGroup("Player");
        
        // Setup anchor points based on initial spawn
        _homeAnchor = GlobalPosition;
        
        // Procedurally generate the other anchors nearby for testing
        float angle1 = (float)GD.RandRange(0, Mathf.Pi * 2);
        _eatAnchor = _homeAnchor + new Vector3(Mathf.Cos(angle1) * 15f, 0, Mathf.Sin(angle1) * 15f);
        
        float angle2 = (float)GD.RandRange(0, Mathf.Pi * 2);
        _workAnchor = _homeAnchor + new Vector3(Mathf.Cos(angle2) * 30f, 0, Mathf.Sin(angle2) * 30f);

        // Phase 22: The Hammam (Wash Station)
        float angle3 = (float)GD.RandRange(0, Mathf.Pi * 2);
        _washAnchor = _homeAnchor + new Vector3(Mathf.Cos(angle3) * 20f, 0, Mathf.Sin(angle3) * 20f);

        _currentAnchor = _homeAnchor;

        // Find dialogue bubble
        _dialogueBubble = GetNodeOrNull<Label3D>("DialogueBubble");
        if (_dialogueBubble != null)
            _dialogueBubble.Visible = false;

        // Proximity zone
        _proximityZone = GetNodeOrNull<Area3D>("ProximityZone");

        // Phase 26: Get Head Controller
        _headController = GetNodeOrNull<HyperRealHead>("HyperRealHead");
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_player == null) return;

        float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);
        ProximityZone zone = GetProximityZone(distance);

        // Phase 24: Deposit Stigmergic Foot Traffic
        if (WorldMemoryField.Instance != null && Velocity.LengthSquared() > 0.1f)
        {
            WorldMemoryField.Instance.DepositFootTraffic(GlobalPosition, dt * 0.1f);
        }

        // Dialogue
        if (zone >= ProximityZone.Conversation && _dialogueTimer <= 0)
        {
            string line = GenerateDialogue(zone);
            ShowDialogue(line);
            _dialogueTimer = 4.0f + (float)GD.RandRange(0, 2);
        }
        _dialogueTimer -= dt;

        // Phase 22: The Hygiene Scourge
        HygieneLevel -= dt * 2.0f; // Constantly gets dirty
        HygieneLevel = Mathf.Max(HygieneLevel, 0);

        if (HygieneLevel <= 0)
        {
            InfectionLevel += dt * 5.0f; // Infection rises when filthy
        }

        if (InfectionLevel >= 100f && !_isScourge)
        {
            MetamorphoseIntoScourge();
        }

        if (_isScourge)
        {
            if (WorldMemoryField.Instance != null)
            {
                WorldMemoryField.Instance.DepositDanger(GlobalPosition, dt * 0.5f);
            }
            HuntCleanNpcs(dt);
            return; // Skip all other logic
        }

        if (NpcRole == "bather" && HygieneLevel < 30f)
        {
            // The Bathing Axiom: Route to wash station immediately
            if (GlobalPosition.DistanceTo(_washAnchor) > 1.5f)
            {
                MoveTowards(_washAnchor, dt, WalkSpeed * 1.5f);
            }
            else
            {
                HygieneLevel += 20f * dt;
                InfectionLevel -= 20f * dt;
                if (HygieneLevel >= 100f)
                {
                    HygieneLevel = 100f;
                    InfectionLevel = 0f;
                    GD.Print($"NPC {NpcName} completed bathing ritual.");
                }
            }
            return; // Skip normal schedule
        }

        // Passively infect others if unclean
        if (InfectionLevel > 50f)
        {
            PassivelyInfectOthers(dt);
        }

        // Phase 21: Preservation Override
        if (NpcRole == "builder")
        {
            if (_repairTarget == null || _repairTarget.Condition >= 100f)
            {
                HuntForRepairs();
            }

            if (_repairTarget != null)
            {
                if (GlobalPosition.DistanceTo(_repairTarget.GlobalPosition) > 2.0f)
                {
                    MoveTowards(_repairTarget.GlobalPosition, dt, WalkSpeed * 1.5f); // Run to fix it
                }
                else
                {
                    _repairTarget.Repair(15.0f * dt); // Repair over time
                    if (_repairTarget.Condition >= 100f)
                    {
                        GD.Print($"NPC {NpcName} finished preserving the property at {_repairTarget.GlobalPosition}.");
                        _repairTarget = null;
                    }
                }
                return; // Skip normal schedule
            }
        }

        // Phase 23: Security Rig Mounting
        if (NpcRole == "security_pilot" || NpcRole == "security_operator")
        {
            var rigs = GetTree().GetNodesInGroup("Vehicles");
            foreach (var node in rigs)
            {
                if (node is SecurityRig rig)
                {
                    if (NpcRole == "security_pilot" && rig.Pilot == null)
                    {
                        if (GlobalPosition.DistanceTo(rig.GlobalPosition) > 2.0f)
                        {
                            MoveTowards(rig.GlobalPosition, dt, WalkSpeed * 1.5f);
                        }
                        else
                        {
                            rig.Pilot = this;
                            GD.Print($"NPC {NpcName} mounted Security Rig as Pilot.");
                        }
                    }
                    else if (NpcRole == "security_operator" && rig.Operator == null)
                    {
                        if (GlobalPosition.DistanceTo(rig.GlobalPosition) > 2.0f)
                        {
                            MoveTowards(rig.GlobalPosition, dt, WalkSpeed * 1.5f);
                        }
                        else
                        {
                            rig.Operator = this;
                            GD.Print($"NPC {NpcName} mounted Security Rig as Operator.");
                        }
                    }

                    // Once mounted, physical position snaps to rig and biological logic suspends
                    if (rig.Pilot == this || rig.Operator == this)
                    {
                        GlobalPosition = rig.GlobalPosition;
                        return; // Suspend normal logic
                    }
                }
            }
        }

        // Phase 17: Route to active biological constraint
        if (zone < ProximityZone.Calling) // Only route when player isn't close/interrupting
        {
            DetermineCurrentAnchor();
            
            // Cinematic Snapping (if the Director scrubs time rapidly)
            if (GlobalPosition.DistanceTo(_currentAnchor) > 50.0f)
            {
                GlobalPosition = _currentAnchor;
                GD.Print($"NPC {NpcName} cinematically snapped to their anchor due to time scrub.");
            }
            else
            {
                // Only move if we aren't already at the checkpoint
                if (GlobalPosition.DistanceTo(_currentAnchor) > 1.5f)
                {
                    // Phase 24: Check Danger Field and avoid if high
                    if (WorldMemoryField.Instance != null && WorldMemoryField.Instance.GetDanger(GlobalPosition) > 1.0f)
                    {
                        // Flee from danger
                        MoveTowards(GlobalPosition + new Vector3((float)GD.RandRange(-1, 1), 0, (float)GD.RandRange(-1, 1)), dt, WalkSpeed * 1.5f);
                    }
                    else
                    {
                        MoveTowards(_currentAnchor, dt, WalkSpeed);
                    }
                }
            }
        }
        else
        {
            // Face the player in conversation range
            if (zone <= ProximityZone.Conversation)
            {
                LookAt(new Vector3(_player.GlobalPosition.X, GlobalPosition.Y, _player.GlobalPosition.Z), Vector3.Up);
                if (_headController != null)
                {
                    // Track camera (assuming player's camera is slightly above player root)
                    _headController.SetTrackingTarget(_player.GlobalPosition + new Vector3(0, 1.6f, 0));
                }
            }
            else
            {
                if (_headController != null)
                {
                    _headController.StopTracking();
                }
            }
        }
    }

    private ProximityZone GetProximityZone(float dist)
    {
        if (dist <= 1.5f) return ProximityZone.Intimate;
        if (dist <= 4.0f) return ProximityZone.Conversation;
        if (dist <= 10.0f) return ProximityZone.Calling;
        if (dist <= 30.0f) return ProximityZone.Distant;
        return ProximityZone.None;
    }

    private string GenerateDialogue(ProximityZone zone)
    {
        // Dialogue is driven by vinculum deviations
        // This maps directly to the design inventory in proximity-dialogue-inventory.md

        // Check dominant vinculum first
        if (ConstraintField.Infection > 0.5f && zone == ProximityZone.Intimate)
            return "...I'm not feeling good. Don't get close.";

        if (ConstraintField.Fatigue > 0.8f)
        {
            return NpcRole switch
            {
                "guard" => "{rubs eyes} Long night. You see anything?",
                _ => "{yawning} When's my shift over..."
            };
        }

        if (ConstraintField.Hunger < 0.2f)
        {
            return NpcRole switch
            {
                "gatherer" => "Roots are thinning out past the creek.",
                _ => "You got anything edible? I'll trade."
            };
        }

        if (ConstraintField.Thirst < 0.2f)
            return "Water's running low. Check the pump.";

        if (ConstraintField.HordeThreat > 0.7f)
            return "You hear that? Something's coming.";

        if (ConstraintField.Morale < 0.3f)
            return "{staring at nothing} ...doesn't feel real.";

        // Default zone-based dialogue
        return zone switch
        {
            ProximityZone.Intimate => "{low voice} Keep your eyes open. Not everyone out here is friendly.",
            ProximityZone.Conversation => $"Name's {NpcName}. Don't cause trouble.",
            ProximityZone.Calling => "Keep moving. Don't stop.",
            ProximityZone.Distant => "Hey! You okay?",
            _ => "..."
        };
    }

    private void ShowDialogue(string text)
    {
        if (_dialogueBubble != null)
        {
            _dialogueBubble.Text = text;
            _dialogueBubble.Visible = true;
            // Auto-hide after timer
            var timer = GetTree().CreateTimer(4.0f);
            timer.Timeout += () => { if (_dialogueBubble != null) _dialogueBubble.Visible = false; };
        }
    }

    public override void OnInteract(InteractionSystem system)
    {
        string line = GenerateDialogue(ProximityZone.Intimate);
        ShowDialogue(line);
    }

    public override string GetPromptText()
    {
        return $"Talk to {NpcName}";
    }

    private void MoveTowards(Vector3 target, double delta, float speed)
    {
        Vector3 dir = (target - GlobalPosition).Normalized();
        
        // Phase 24: Stigmergic Pathing
        if (WorldMemoryField.Instance != null && !_isScourge)
        {
            Vector3 trailForce = WorldMemoryField.Instance.GetTrailGradient(GlobalPosition);
            if (trailForce.LengthSquared() > 0)
            {
                // Blend the direct path with the established trail
                dir = (dir * 0.7f + trailForce * 0.3f).Normalized();
            }
        }

        Velocity = new Vector3(dir.X * speed, Velocity.Y, dir.Z * speed);
        MoveAndSlide();
    }

    private void DetermineCurrentAnchor()
    {
        float time = ConstraintField.TimeOfDay;

        if (time >= 22.0f || time <= 6.0f)
        {
            // Sleep
            _currentAnchor = _homeAnchor;
        }
        else if ((time >= 7.0f && time <= 8.5f) || (time >= 18.0f && time <= 19.5f))
        {
            // Eat
            _currentAnchor = _eatAnchor;
        }
        else
        {
            // Work (Bare minimum activity)
            _currentAnchor = _workAnchor;
        }
    }

    private void HuntForRepairs()
    {
        if (PropagationSystem.Instance == null) return;
        
        var properties = GetTree().GetNodesInGroup("Properties");
        foreach (var p in properties)
        {
            if (p is PropertyIntegrity prop)
            {
                if (prop.Condition < 50.0f)
                {
                    _repairTarget = prop;
                    GD.Print($"NPC {NpcName} broke schedule to perform Property Preservation at {_repairTarget.GlobalPosition}.");
                    break;
                }
            }
        }
    }

    // Phase 22 Logic
    private void MetamorphoseIntoScourge()
    {
        _isScourge = true;
        NpcRole = "scourge";
        WalkSpeed *= 1.8f; // Fast zombies
        
        // Visual indicator
        var mesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        if (mesh != null && mesh.MaterialOverride == null)
        {
            mesh.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.8f, 0.1f) };
        }

        GD.Print($"[SCOURGE]: {NpcName} has succumbed to the infection and metamorphosed.");
        if (PropagationSystem.Instance != null)
        {
            PropagationSystem.Instance.SpikeDataDensity(GlobalPosition, 500f); // Draw cameras to the transformation
        }
    }

    private void HuntCleanNpcs(float dt)
    {
        // Phase 23: Distraction Aggro Override
        if (SecurityRig.ActiveDistraction != null && SecurityRig.ActiveDistraction.IsDistracting)
        {
            MoveTowards(SecurityRig.ActiveDistraction.GlobalPosition, dt, WalkSpeed);
            return; // Horde is fully distracted by the rig
        }

        var npcs = GetTree().GetNodesInGroup("NPCs");
        Node3D target = null;
        float closestDist = 100f;

        foreach (var node in npcs)
        {
            if (node is NPC other && !other._isScourge)
            {
                float d = GlobalPosition.DistanceTo(other.GlobalPosition);
                if (d < closestDist)
                {
                    closestDist = d;
                    target = other;
                }
            }
        }

        if (target != null)
        {
            MoveTowards(target.GlobalPosition, dt, WalkSpeed);
            if (closestDist < 1.5f && target is NPC victim)
            {
                victim.InfectionLevel += 50f * dt; // Bite them
            }
        }
    }

    private void PassivelyInfectOthers(float dt)
    {
        var npcs = GetTree().GetNodesInGroup("NPCs");
        foreach (var node in npcs)
        {
            if (node is NPC other && !other._isScourge && other != this)
            {
                if (GlobalPosition.DistanceTo(other.GlobalPosition) < 5.0f)
                {
                    other.InfectionLevel += 10f * dt; // Airborne transmission
                }
            }
        }
    }

    /// <summary>
    /// Narrative evaluation hook. Triggers a cutscene pan when significant NPCs die.
    /// </summary>
    public void Die()
    {
        if (NpcRole == "guard")
        {
            // Pause gameplay
            GetTree().Paused = true;
            
            // Retrieve player's camera
            var camera = _player?.GetNodeOrNull<Camera3D>("Camera3D");
            if (camera != null)
            {
                var tween = GetTree().CreateTween();
                tween.SetPauseMode(Tween.TweenPauseMode.Process); // Ensure tween runs while game is paused
                tween.TweenProperty(camera, "global_position", GlobalPosition + new Vector3(0, 2, 5), 2.0f);
                
                // Show narrative text
                ShowDialogue($"[System]: Constraint Broken. {NpcName} has died.");
            }
            GD.Print($"Narrative Trigger: {NpcName} has died. Panning camera.");
        }
        QueueFree();
    }
}
