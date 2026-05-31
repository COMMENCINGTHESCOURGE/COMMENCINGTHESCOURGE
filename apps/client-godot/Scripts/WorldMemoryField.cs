using Godot;
using System.Collections.Generic;

/// <summary>
/// Phase 24: The World Memory Field (Stigmergic Terrain Integration)
/// The terrain remembers what happens. Agents write to this field, and read from it.
/// </summary>
public partial class WorldMemoryField : Node3D
{
    public static WorldMemoryField Instance { get; private set; }

    public class MemoryCell
    {
        public float FootTraffic = 0f;
        public float DangerScent = 0f;
        public float CivAnchor = 0f;
        public float DataDensity = 0f; // Bridging old PropagationSystem metric
    }

    private Dictionary<Vector2I, MemoryCell> _grid = new();
    
    [Export] public float CellSize = 2.0f; // Each cell is 2x2 meters

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        
        // Decay logic (erosion of memory)
        List<Vector2I> keysToRemove = new List<Vector2I>();
        foreach (var kvp in _grid)
        {
            var cell = kvp.Value;
            
            // Foot traffic degrades very slowly (hardened paths stay)
            if (cell.FootTraffic > 0) cell.FootTraffic -= 0.05f * dt;
            
            // Danger scent degrades moderately
            if (cell.DangerScent > 0) cell.DangerScent -= 0.2f * dt;
            
            // Civ Anchor degrades slowly (buildings decay)
            if (cell.CivAnchor > 0) cell.CivAnchor -= 0.02f * dt;
            
            // Data Density degrades fast (tension passes)
            if (cell.DataDensity > 0) cell.DataDensity -= 0.5f * dt;

            // Clamp to zero
            cell.FootTraffic = Mathf.Max(0, cell.FootTraffic);
            cell.DangerScent = Mathf.Max(0, cell.DangerScent);
            cell.CivAnchor = Mathf.Max(0, cell.CivAnchor);
            cell.DataDensity = Mathf.Max(0, cell.DataDensity);

            if (cell.FootTraffic == 0 && cell.DangerScent == 0 && cell.CivAnchor == 0 && cell.DataDensity == 0)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _grid.Remove(key);
        }
    }

    public Vector2I WorldToGrid(Vector3 pos)
    {
        return new Vector2I(Mathf.FloorToInt(pos.X / CellSize), Mathf.FloorToInt(pos.Z / CellSize));
    }

    private MemoryCell GetOrCreateCell(Vector2I gridPos)
    {
        if (!_grid.TryGetValue(gridPos, out var cell))
        {
            cell = new MemoryCell();
            _grid[gridPos] = cell;
        }
        return cell;
    }

    public void DepositFootTraffic(Vector3 pos, float amount = 0.1f)
    {
        var cell = GetOrCreateCell(WorldToGrid(pos));
        cell.FootTraffic = Mathf.Min(10.0f, cell.FootTraffic + amount);
    }

    public void DepositDanger(Vector3 pos, float amount = 0.5f)
    {
        var cell = GetOrCreateCell(WorldToGrid(pos));
        cell.DangerScent = Mathf.Min(5.0f, cell.DangerScent + amount);
    }

    public void DepositCivAnchor(Vector3 pos, float amount = 1.0f)
    {
        var cell = GetOrCreateCell(WorldToGrid(pos));
        cell.CivAnchor = Mathf.Min(10.0f, cell.CivAnchor + amount);
    }
    
    public void SpikeDataDensity(Vector3 pos, float amount)
    {
        var cell = GetOrCreateCell(WorldToGrid(pos));
        cell.DataDensity = Mathf.Min(1000f, cell.DataDensity + amount);
        
        // Also fire off the old propagation system for legacy hooks
        if (PropagationSystem.Instance != null)
        {
            PropagationSystem.Instance.SpikeDataDensity(pos, amount);
        }
    }

    public float GetFootTraffic(Vector3 pos)
    {
        if (_grid.TryGetValue(WorldToGrid(pos), out var cell)) return cell.FootTraffic;
        return 0f;
    }

    public float GetDanger(Vector3 pos)
    {
        if (_grid.TryGetValue(WorldToGrid(pos), out var cell)) return cell.DangerScent;
        return 0f;
    }
    
    public float GetCivAnchor(Vector3 pos)
    {
        if (_grid.TryGetValue(WorldToGrid(pos), out var cell)) return cell.CivAnchor;
        return 0f;
    }

    /// <summary>
    /// Stigmergic pathfinding: Returns a gradient direction pointing toward higher foot traffic.
    /// </summary>
    public Vector3 GetTrailGradient(Vector3 pos)
    {
        Vector2I gp = WorldToGrid(pos);
        float center = GetFootTraffic(pos);
        
        float right = _grid.TryGetValue(gp + new Vector2I(1, 0), out var cR) ? cR.FootTraffic : 0;
        float left = _grid.TryGetValue(gp + new Vector2I(-1, 0), out var cL) ? cL.FootTraffic : 0;
        float up = _grid.TryGetValue(gp + new Vector2I(0, 1), out var cU) ? cU.FootTraffic : 0;
        float down = _grid.TryGetValue(gp + new Vector2I(0, -1), out var cD) ? cD.FootTraffic : 0;

        float gradX = right - left;
        float gradZ = up - down; // Z is up in grid space here

        if (Mathf.Abs(gradX) < 0.01f && Mathf.Abs(gradZ) < 0.01f) return Vector3.Zero;

        return new Vector3(gradX, 0, gradZ).Normalized();
    }
}
