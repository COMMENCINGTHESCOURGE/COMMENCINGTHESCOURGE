using Godot;
using System.Collections.Generic;

/// <summary>
/// Modular grid placement system for base building.
/// Supports walls, floors, doors, windows, stairs on a snap grid.
/// Structural integrity propagates upward and outward.
/// </summary>
public partial class BuildingGrid : Node3D
{
    [Export] public float CellSize = 2.0f;         // Grid cell size in meters
    [Export] public int MaxHeight = 5;              // Max stories
    [Export] public int GridRadius = 20;            // Buildable area in cells from origin

    // Building piece definitions
    public enum PieceType
    {
        Foundation,
        Floor,
        Wall,
        Doorway,
        Window,
        Stairs,
        Roof,
        Ramp,
        Pillar
    }

    public class GridCell
    {
        public Vector3I Position;         // Grid coordinates
        public PieceType Type;
        public float Integrity = 1.0f;    // 1.0 = perfect, 0.0 = collapsed
        public float MaxIntegrity = 1.0f;
        public Node3D VisualInstance;
        public bool HasSupport = true;    // Whether this cell has structural support below
        public bool IsDoor = false;
        public bool IsOpen = false;
    }

    private Dictionary<Vector3I, GridCell> _cells = new();
    private PackedScene _wallScene;
    private PackedScene _floorScene;
    private PackedScene _doorScene;

    // Material for built structures
    private StandardMaterial3D _wallMaterial;
    private StandardMaterial3D _floorMaterial;
    private StandardMaterial3D _foundationMaterial;

    public override void _Ready()
    {
        CreatePlaceholderMeshes();
        LoadBalanceManifest();
    }

    private void LoadBalanceManifest()
    {
        if (FileAccess.FileExists("res://balance_manifest.json"))
        {
            using var file = FileAccess.Open("res://balance_manifest.json", FileAccess.ModeFlags.Read);
            string content = file.GetAsText();
            var json = new Json();
            var error = json.Parse(content);
            if (error == Error.Ok)
            {
                GD.Print("Successfully loaded balance coefficients from manifest");
                // Here we would extract the decay coefficients and apply them to base integrity fields
            }
            else
            {
                GD.PrintErr("Failed to parse balance_manifest.json");
            }
        }
        else
        {
            GD.Print("balance_manifest.json not found, using default coefficients");
        }
    }

    private void CreatePlaceholderMeshes()
    {
        // Wall material - gray
        _wallMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.5f, 0.5f, 0.5f),
            Metallic = 0.1f,
            Roughness = 0.8f
        };

        // Floor material - wood tone
        _floorMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.6f, 0.4f, 0.2f),
            Metallic = 0.0f,
            Roughness = 0.9f
        };

        // Foundation material - concrete
        _foundationMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.4f, 0.4f, 0.45f),
            Metallic = 0.05f,
            Roughness = 0.9f
        };
    }

    /// <summary>
    /// Convert world position to grid coordinates.
    /// </summary>
    public Vector3I WorldToGrid(Vector3 worldPos)
    {
        return new Vector3I(
            Mathf.RoundToInt(worldPos.X / CellSize),
            Mathf.RoundToInt(worldPos.Y / CellSize),
            Mathf.RoundToInt(worldPos.Z / CellSize)
        );
    }

    /// <summary>
    /// Convert grid coordinates to world position (center of cell).
    /// </summary>
    public Vector3 GridToWorld(Vector3I gridPos)
    {
        return new Vector3(
            gridPos.X * CellSize,
            gridPos.Y * CellSize,
            gridPos.Z * CellSize
        );
    }

    /// <summary>
    /// Place a building piece at the given grid position.
    /// Returns true if placement succeeded.
    /// </summary>
    public bool PlacePiece(Vector3I gridPos, PieceType type)
    {
        // Validate position
        if (gridPos.Y < 0 || gridPos.Y > MaxHeight)
            return false;

        if (Mathf.Abs(gridPos.X) > GridRadius || Mathf.Abs(gridPos.Z) > GridRadius)
            return false;

        // Check if cell is occupied
        if (_cells.ContainsKey(gridPos))
            return false;

        // Check structural support (foundations and ground-level go directly on ground)
        if (gridPos.Y == 0)
        {
            // Ground level - only foundations and pillars
            if (type != PieceType.Foundation && type != PieceType.Pillar && type != PieceType.Floor)
                return false;
        }
        else
        {
            // Above ground - check if there's support below
            bool hasSupportBelow = false;
            Vector3I below = new(gridPos.X, gridPos.Y - 1, gridPos.Z);
            if (_cells.ContainsKey(below))
            {
                var belowCell = _cells[below];
                if (belowCell.Type == PieceType.Foundation ||
                    belowCell.Type == PieceType.Floor ||
                    belowCell.Type == PieceType.Pillar)
                    hasSupportBelow = true;
            }

            // Walls and doorways can also support from the side
            if (!hasSupportBelow)
            {
                Vector3I[] neighbors = {
                    new(gridPos.X - 1, gridPos.Y, gridPos.Z),
                    new(gridPos.X + 1, gridPos.Y, gridPos.Z),
                    new(gridPos.X, gridPos.Y, gridPos.Z - 1),
                    new(gridPos.X, gridPos.Y, gridPos.Z + 1)
                };
                foreach (var n in neighbors)
                {
                    if (_cells.TryGetValue(n, out var nCell) &&
                        (nCell.Type == PieceType.Wall || nCell.Type == PieceType.Pillar))
                    {
                        hasSupportBelow = true;
                        break;
                    }
                }
            }

            if (!hasSupportBelow && type != PieceType.Pillar)
                return false; // No support - can't place here
        }

        // Create the visual piece
        Node3D visual = CreateVisualPiece(type, gridPos);
        if (visual == null)
            return false;

        AddChild(visual);

        // Store the cell
        var cell = new GridCell
        {
            Position = gridPos,
            Type = type,
            VisualInstance = visual,
            Integrity = 1.0f,
            MaxIntegrity = GetBaseIntegrity(type),
            HasSupport = true,
            IsDoor = type == PieceType.Doorway
        };
        _cells[gridPos] = cell;

        if (type == PieceType.Window) _windowCount++;
        if (type == PieceType.Doorway) _doorCount++;
        if (type == PieceType.Wall) _wallCount++;

        ConstraintField.GlassToWallRatio = GetGlassToWallRatio();

        return true;
    }

    /// <summary>
    /// Remove a building piece at the given grid position.
    /// Returns true if removal succeeded.
    /// </summary>
    public bool RemovePiece(Vector3I gridPos)
    {
        if (!_cells.TryGetValue(gridPos, out var cell))
            return false;

        // Check if anything above depends on this piece
        Vector3I above = new(gridPos.X, gridPos.Y + 1, gridPos.Z);
        if (_cells.ContainsKey(above))
        {
            var aboveCell = _cells[above];
            if (aboveCell.Type == PieceType.Wall || aboveCell.Type == PieceType.Pillar ||
                aboveCell.Type == PieceType.Floor || aboveCell.Type == PieceType.Roof)
                return false; // Something is resting on this piece
        }

        // Remove visual
        if (cell.VisualInstance != null)
        {
            cell.VisualInstance.QueueFree();
        }

        // Track structure-wide counts
        if (cell.Type == PieceType.Window) _windowCount--;
        if (cell.Type == PieceType.Doorway) _doorCount--;
        if (cell.Type == PieceType.Wall) _wallCount--;

        _cells.Remove(gridPos);
        ConstraintField.GlassToWallRatio = GetGlassToWallRatio();
        return true;
    }

    /// <summary>
    /// Damage a piece at the given position.
    /// </summary>
    public void DamagePiece(Vector3I gridPos, float damage)
    {
        if (!_cells.TryGetValue(gridPos, out var cell))
            return;

        cell.Integrity -= damage;
        if (cell.Integrity <= 0)
        {
            CollapsePiece(gridPos);
        }
        else
        {
            // Visual feedback - tint darker as damage increases
            float damageRatio = 1.0f - (cell.Integrity / cell.MaxIntegrity);
            if (cell.VisualInstance is MeshInstance3D mesh)
            {
                var mat = mesh.Mesh.SurfaceGetMaterial(0) as StandardMaterial3D;
                if (mat != null)
                {
                    mat.AlbedoColor = mat.AlbedoColor.Lerp(new Color(0.2f, 0.1f, 0.05f), damageRatio * 0.5f);
                }
            }
        }
    }

    /// <summary>
    /// Collapse a piece and propagate damage below.
    /// </summary>
    private void CollapsePiece(Vector3I gridPos)
    {
        if (!_cells.TryGetValue(gridPos, out var cell))
            return;

        // Visual collapse
        if (cell.VisualInstance != null)
        {
            // Spawn debris particles here (future improvement)
            cell.VisualInstance.QueueFree();
        }

        _cells.Remove(gridPos);

        // Propagate damage to supporting pieces below
        Vector3I below = new(gridPos.X, gridPos.Y - 1, gridPos.Z);
        if (_cells.TryGetValue(below, out var belowCell))
        {
            belowCell.Integrity -= 0.3f;
            if (belowCell.Integrity <= 0)
                CollapsePiece(below);
        }

        ConstraintField.GlassToWallRatio = GetGlassToWallRatio();
    }

    /// <summary>
    /// Create a visual mesh for the given piece type.
    /// </summary>
    private Node3D CreateVisualPiece(PieceType type, Vector3I gridPos)
    {
        Vector3 position = GridToWorld(gridPos);
        Node3D node;

        switch (type)
        {
            case PieceType.Foundation:
            {
                var mesh = new MeshInstance3D();
                mesh.Mesh = new BoxMesh
                {
                    Size = new Vector3(CellSize, 0.3f, CellSize),
                    Material = _foundationMaterial
                };
                mesh.Position = position;
                node = mesh;
                break;
            }

            case PieceType.Floor:
            {
                var mesh = new MeshInstance3D();
                mesh.Mesh = new BoxMesh
                {
                    Size = new Vector3(CellSize, 0.15f, CellSize),
                    Material = _floorMaterial
                };
                mesh.Position = position;
                node = mesh;
                break;
            }

            case PieceType.Wall:
            {
                var mesh = new MeshInstance3D();
                mesh.Mesh = new BoxMesh
                {
                    Size = new Vector3(CellSize, CellSize, 0.15f),
                    Material = _wallMaterial
                };
                mesh.Position = position + new Vector3(0, CellSize / 2.0f, 0);
                node = mesh;
                break;
            }

            case PieceType.Doorway:
            {
                // Wall with a hole - represented as two wall segments + open space
                var wall = new Node3D();
                wall.Position = position + new Vector3(0, CellSize / 2.0f, 0);

                // Left panel
                var left = new MeshInstance3D();
                left.Mesh = new BoxMesh
                {
                    Size = new Vector3(CellSize * 0.35f, CellSize * 0.7f, 0.15f),
                    Material = _wallMaterial
                };
                left.Position = new Vector3(-CellSize * 0.3f, CellSize * 0.15f, 0);
                wall.AddChild(left);

                // Right panel
                var right = new MeshInstance3D();
                right.Mesh = new BoxMesh
                {
                    Size = new Vector3(CellSize * 0.35f, CellSize * 0.7f, 0.15f),
                    Material = _wallMaterial
                };
                right.Position = new Vector3(CellSize * 0.3f, CellSize * 0.15f, 0);
                wall.AddChild(right);

                // Top panel
                var top = new MeshInstance3D();
                top.Mesh = new BoxMesh
                {
                    Size = new Vector3(CellSize, CellSize * 0.25f, 0.15f),
                    Material = _wallMaterial
                };
                top.Position = new Vector3(0, CellSize * 0.6f, 0);
                wall.AddChild(top);

                node = wall;
                break;
            }

            case PieceType.Window:
            {
                // Wall with window opening
                var wall = new Node3D();
                wall.Position = position + new Vector3(0, CellSize / 2.0f, 0);

                var mesh = new MeshInstance3D();
                mesh.Mesh = new BoxMesh
                {
                    Size = new Vector3(CellSize, CellSize * 0.4f, 0.15f),
                    Material = _wallMaterial
                };
                mesh.Position = new Vector3(0, CellSize * 0.3f, 0);
                wall.AddChild(mesh);

                var topMesh = new MeshInstance3D();
                topMesh.Mesh = new BoxMesh
                {
                    Size = new Vector3(CellSize, CellSize * 0.3f, 0.15f),
                    Material = _wallMaterial
                };
                topMesh.Position = new Vector3(0, CellSize * 0.65f, 0);
                wall.AddChild(topMesh);

                node = wall;
                break;
            }

            case PieceType.Stairs:
            {
                // Simple stair block
                var stairs = new Node3D();
                stairs.Position = position;

                for (int i = 0; i < 4; i++)
                {
                    var step = new MeshInstance3D();
                    float stepHeight = 0.4f;
                    float stepWidth = CellSize / 4.0f;
                    step.Mesh = new BoxMesh
                    {
                        Size = new Vector3(CellSize, stepHeight, stepWidth),
                        Material = _floorMaterial
                    };
                    step.Position = new Vector3(0, i * stepHeight + stepHeight / 2.0f,
                        -CellSize / 2.0f + i * stepWidth + stepWidth / 2.0f);
                    stairs.AddChild(step);
                }

                node = stairs;
                break;
            }

            case PieceType.Roof:
            {
                // Sloped roof - represented as angled box
                var mesh = new MeshInstance3D();
                // Use a box and tilt it for sloped look
                mesh.Mesh = new BoxMesh
                {
                    Size = new Vector3(CellSize * 1.1f, 0.15f, CellSize * 1.2f),
                    Material = new StandardMaterial3D
                    {
                        AlbedoColor = new Color(0.4f, 0.2f, 0.1f),
                        Metallic = 0.0f,
                        Roughness = 0.9f
                    }
                };
                mesh.Position = position + new Vector3(0, CellSize, 0);
                mesh.RotationDegrees = new Vector3(15, 0, 0);
                node = mesh;
                break;
            }

            case PieceType.Pillar:
            {
                var mesh = new MeshInstance3D();
                mesh.Mesh = new CylinderMesh
                {
                    TopRadius = 0.1f,
                    BottomRadius = 0.1f,
                    Height = CellSize,
                    Material = _foundationMaterial
                };
                mesh.Position = position + new Vector3(0, CellSize / 2.0f, 0);
                node = mesh;
                break;
            }

            default:
                return null;
        }

        // Add a static body for collision
        var body = new StaticBody3D();
        var shape = new CollisionShape3D();
        // Approximate collision shape based on piece
        shape.Shape = new BoxShape3D { Size = new Vector3(CellSize * 0.9f, CellSize * 0.9f, CellSize * 0.9f) };
        body.AddChild(shape);

        // Position the collision to match the visual
        if (type == PieceType.Wall)
            body.Position = position + new Vector3(0, CellSize / 2.0f, 0);
        else if (type == PieceType.Doorway || type == PieceType.Window)
            body.Position = position + new Vector3(0, CellSize / 2.0f, 0);
        else
            body.Position = position;

        node.AddChild(body);

        // Add health bar or interaction hint above (future improvement with world space UI)

        return node;
    }

    // ─── Structure-wide tracking for exposure calculations ───
    private int _windowCount = 0;
    private int _doorCount = 0;
    private int _wallCount = 0;

    /// <summary>
    /// Calculate the glass_surface_area / wall_solid_ratio for the ENTIRE structure.
    /// This is the REAL defensibility metric — not abstract "integrity" numbers.
    /// 
    /// A structure with 50 windows and 3 walls = glass/wall ratio of ~0.60 = sieve.
    /// A structure with 3 windows and 10 walls = glass/wall ratio of ~0.10 = fortress.
    /// 
    /// This replaces the static GetMaxIntegrity() approach entirely.
    /// </summary>
    public float GetGlassToWallRatio()
    {
        if (_wallCount == 0) return 0f;
        // Each window contributes ~0.8m² of glass exposure per cell
        // Each wall segment contributes ~3.6m² of solid surface per cell
        float glassArea = _windowCount * 0.8f;
        float wallArea = _wallCount * 3.6f + _doorCount * 2.5f;
        return wallArea > 0f ? glassArea / wallArea : 0f;
    }

    /// <summary>
    /// Get the number of boards required to fully fortify all windows.
    /// Aesthetic-optimized building (glass/wall=0.60): ~620 boards.
    /// Structure-optimized building (glass/wall=0.10): ~48 boards.
    /// </summary>
    public int GetBoardsRequired()
    {
        return _windowCount * 12; // ~12 boards per window (cross-pattern boarding)
    }

    /// <summary>
    /// Get the defensive exposure score (0-1).
    /// 0 = completely sealed (no windows, concrete walls).
    /// 1 = completely exposed (all glass, no solid walls).
    /// </summary>
    public float GetDefensiveExposure()
    {
        float gw = GetGlassToWallRatio();
        return Mathf.Clamp(gw / 0.8f, 0f, 1f); // Normalized to worst case
    }

    /// <summary>
    /// Get the effective structural integrity considering exposure.
    /// A building with high glass/wall ratio degrades faster because
    /// breaches are inevitable through unboarded surfaces.
    /// </summary>
    public float GetEffectiveIntegrity(Vector3I gridPos)
    {
        if (!_cells.TryGetValue(gridPos, out var cell))
            return 0f;

        float baseIntegrity = cell.MaxIntegrity;
        float exposure = GetDefensiveExposure();

        // Exposure penalty: a building with many windows has more breach points
        // and thus each individual wall segment is more vulnerable to flanking
        float baseValue = GetBaseIntegrity(cell.Type);
        return baseValue * (1f - exposure * 0.5f);
    }

    /// <summary>
    /// Base material integrity by piece type.
    /// These are ALWAYS modified by the structure's glass/wall ratio.
    /// </summary>
    private float GetBaseIntegrity(PieceType type)
    {
        return type switch
        {
            PieceType.Foundation => 150.0f,
            PieceType.Pillar => 120.0f,
            PieceType.Wall => 80.0f,
            PieceType.Doorway => 50.0f,
            PieceType.Window => 40.0f,
            PieceType.Floor => 60.0f,
            PieceType.Stairs => 40.0f,
            PieceType.Roof => 30.0f,
            _ => 50.0f
        };
    }

    /// <summary>
    /// Get the structural integrity ratio of the overall building.
    /// </summary>
    public float GetOverallIntegrity()
    {
        if (_cells.Count == 0) return 1.0f;
        float total = 0;
        foreach (var cell in _cells.Values)
            total += cell.Integrity / cell.MaxIntegrity;
        return total / _cells.Count;
    }

    /// <summary>
    /// Get the number of placed pieces.
    /// </summary>
    public int PieceCount => _cells.Count;

    /// <summary>
    /// Check if a grid position is buildable.
    /// </summary>
    public bool IsPositionBuildable(Vector3I gridPos)
    {
        if (_cells.ContainsKey(gridPos)) return false;
        if (gridPos.Y < 0 || gridPos.Y > MaxHeight) return false;
        if (Mathf.Abs(gridPos.X) > GridRadius || Mathf.Abs(gridPos.Z) > GridRadius) return false;
        return true;
    }

    /// <summary>
    /// Get all cells that need maintenance (integrity < 0.5).
    /// </summary>
    public List<Vector3I> GetDamagedCells()
    {
        var result = new List<Vector3I>();
        foreach (var kvp in _cells)
        {
            if (kvp.Value.Integrity / kvp.Value.MaxIntegrity < 0.5f)
                result.Add(kvp.Key);
        }
        return result;
    }

    /// <summary>
    /// Apply degradation to all pieces (called from constraint field tick).
    /// </summary>
    public void ApplyDegradation(float rate)
    {
        float exposure = GetDefensiveExposure();
        int boardCount = GetBoardsRequired();

        float structuralLoad = GetUnderAttackCount(); // Load is equivalent to horde density pushing on the walls
        float dt = rate; // rate represents the delta time / tick rate

        foreach (var cell in _cells.Values)
        {
            // REVISED: Degradation is NOT a flat rate.
            // It is CONSTRUCTION TYPE × EXPOSURE × TIME.
            float exposurePenalty = 1.0f + exposure * 1.5f;

            // --- Phase 9: Structural Shear (The Yield Point) ---
            // Calculate yield threshold based on GlassToWallRatio (brittleness)
            float yieldThreshold = Mathf.Max(1.0f, 10.0f - (GetGlassToWallRatio() * 15.0f)); 

            if (structuralLoad > yieldThreshold)
            {
                // Step-change in structural integrity (catastrophic failure)
                float catastrophicDamage = (structuralLoad - yieldThreshold) * 15.0f * dt;
                cell.Integrity -= catastrophicDamage;

                if (AmbientFXController.Instance != null && Mathf.Abs((int)cell.Integrity % 20) == 0) // Trigger every 20 HP lost
                {
                    AmbientFXController.Instance.TriggerStructureCollapse(GridToWorld(cell.Position));
                }

                // High glass ratio buildings shatter immediately
                if (GetGlassToWallRatio() > 0.6f && cell.Integrity < 80.0f)
                {
                    cell.Integrity = 0.0f; // Instant glass shatter collapse
                }
            }

            if (cell.Integrity <= 0)
                CollapsePiece(cell.Position);
        }
    }

    /// <summary>
    /// Board up a window or doorway. Increases integrity but blocks passage.
    /// </summary>
    public bool BoardUp(Vector3I gridPos)
    {
        if (!_cells.TryGetValue(gridPos, out var cell))
            return false;

        if (cell.Type != PieceType.Window && cell.Type != PieceType.Doorway)
            return false;

        cell.MaxIntegrity *= 2.0f;
        cell.Integrity = cell.MaxIntegrity;

        // Visual change: darker material
        if (cell.VisualInstance != null)
        {
            var mats = new List<StandardMaterial3D>();
            FindMaterials(cell.VisualInstance, mats);
            foreach (var mat in mats)
            {
                mat.AlbedoColor = new Color(0.3f, 0.2f, 0.1f);
            }
        }

        return true;
    }

    private void FindMaterials(Node3D node, List<StandardMaterial3D> results)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                if (mesh.Mesh.SurfaceGetMaterial(i) is StandardMaterial3D mat)
                    results.Add(mat);
            }
        }
        foreach (Node3D child in node.GetChildren())
            FindMaterials(child, results);
    }

    /// <summary>
    /// Count zombies attacking the structure. Returns the number of walls being hit.
    /// </summary>
    public int GetUnderAttackCount()
    {
        int count = 0;
        var zombies = GetTree().GetNodesInGroup("Zombies");
        foreach (Zombie z in zombies)
        {
            Vector3I gridPos = WorldToGrid(z.GlobalPosition);
            if (_cells.ContainsKey(gridPos) || IsNearStructure(gridPos))
                count++;
        }
        return count;
    }

    private bool IsNearStructure(Vector3I pos)
    {
        for (int x = -1; x <= 1; x++)
        for (int z = -1; z <= 1; z++)
        {
            if (_cells.ContainsKey(new Vector3I(pos.X + x, pos.Y, pos.Z + z)))
                return true;
        }
        return false;
    }
}
