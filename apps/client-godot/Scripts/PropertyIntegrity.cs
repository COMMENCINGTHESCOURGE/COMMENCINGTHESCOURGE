using Godot;

/// <summary>
/// Phase 21: Property Preservation (Entropy)
/// Tracks degradation of geometry and calls for Maintenance AI.
/// </summary>
public partial class PropertyIntegrity : Node3D
{
    [Export] public float Condition = 100.0f;
    [Export] public float DegradationRate = 2.0f; // condition lost per second during bad weather
    
    private bool _isRuined = false;
    private Color _originalColor;
    private CsgBox3D _parentMesh;
    private float _scentTimer = 0.0f;

    public override void _Ready()
    {
        _parentMesh = GetParentOrNull<CsgBox3D>();
        if (_parentMesh != null && _parentMesh.MaterialOverride is StandardMaterial3D mat)
        {
            _originalColor = mat.AlbedoColor;
        }
        AddToGroup("Properties");
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        
        // Ambient Degradation
        if (ConstraintField.SurfaceTractionModifier < 0.5f) // Snow is active
        {
            Condition -= DegradationRate * dt;
        }

        Condition = Mathf.Clamp(Condition, 0, 100);

        // Visual Degradation Hook
        if (Condition <= 0 && !_isRuined)
        {
            _isRuined = true;
            if (_parentMesh != null && _parentMesh.MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = new Color(0.2f, 0.1f, 0.1f); // Rusted/Boarded up look
            }
            GD.Print($"[Property Integrity]: Property at {GlobalPosition} has collapsed to 0% condition.");
        }

        // Emit Maintenance Scent
        if (Condition < 50.0f && Condition > 0.0f)
        {
            _scentTimer -= dt;
            if (_scentTimer <= 0)
            {
                if (PropagationSystem.Instance != null)
                {
                    // Emit a specific data density marker that builders look for
                    PropagationSystem.Instance.SpikeDataDensity(GlobalPosition, 100.0f); 
                }
                _scentTimer = 5.0f; // Broadcast every 5 seconds
            }
        }
    }

    public void Repair(float amount)
    {
        Condition += amount;
        Condition = Mathf.Clamp(Condition, 0, 100);

        if (Condition > 0 && _isRuined)
        {
            _isRuined = false;
            if (_parentMesh != null && _parentMesh.MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = _originalColor; // Restore visual condition
            }
            GD.Print($"[Property Integrity]: Property restored from ruin.");
        }
    }
}
