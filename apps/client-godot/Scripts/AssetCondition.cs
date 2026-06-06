using Godot;

/// <summary>
/// Phase 32: Universal Asset Degradation
/// Attach to any physical object. Decays based on environmental factors (WorldMemoryField).
/// </summary>
public partial class AssetCondition : Node3D
{
    [Export] public float Condition = 100.0f;
    [Export] public float BaseDecayRate = 0.1f; 
    
    private bool _isRuined = false;
    private Color _originalColor;
    private MeshInstance3D _mesh;
    private CollisionShape3D _collider;
    private GpuParticles3D _smokeParticles;

    public override void _Ready()
    {
        // Try to find the mesh
        _mesh = GetParentOrNull<MeshInstance3D>();
        if (_mesh == null) _mesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
        
        if (_mesh != null && _mesh.MaterialOverride is StandardMaterial3D mat)
        {
            _originalColor = mat.AlbedoColor;
        }

        // Try to find collider
        _collider = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
        if (_collider == null) _collider = GetParentOrNull<CollisionShape3D>();

        // Optional Smoke Particles
        _smokeParticles = GetNodeOrNull<GpuParticles3D>("SmokeParticles");
        if (_smokeParticles != null) _smokeParticles.Emitting = false;

        AddToGroup("Assets"); // Universal group for builders
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        
        // Universal Environmental Decay
        float decayMultiplier = 1.0f;
        
        if (WorldMemoryField.Instance != null)
        {
            // Scourge infection rots structures
            float danger = WorldMemoryField.Instance.GetDanger(GlobalPosition);
            decayMultiplier += danger * 2.0f;

            // Civ anchors prevent decay
            float civ = WorldMemoryField.Instance.GetCivAnchor(GlobalPosition);
            decayMultiplier -= civ * 0.5f;
            
            decayMultiplier = Mathf.Max(0.1f, decayMultiplier); // Always decay slightly
        }

        Condition -= BaseDecayRate * decayMultiplier * dt;
        Condition = Mathf.Clamp(Condition, 0, 100);

        // State Metamorphosis (Ruined)
        if (Condition <= 0 && !_isRuined)
        {
            _isRuined = true;
            if (_mesh != null && _mesh.MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = new Color(0.1f, 0.1f, 0.1f); // Rusted / Charred
            }
            if (_collider != null)
            {
                _collider.Disabled = true; // Breach!
            }
            if (_smokeParticles != null)
            {
                _smokeParticles.Emitting = false; // It's completely dead now
            }
            GD.Print($"[Asset Condition]: Asset at {GlobalPosition} has collapsed.");
        }

        // Damaged state (Needs repair)
        if (Condition < 50.0f && Condition > 0.0f && !_isRuined)
        {
            if (_smokeParticles != null && !_smokeParticles.Emitting)
            {
                _smokeParticles.Emitting = true;
            }
            
            // Broadcast need for repair to the Stigmergic field
            if (WorldMemoryField.Instance != null)
            {
                // Spike data density occasionally to draw builders
                if (GD.Randf() < 0.01f)
                {
                    WorldMemoryField.Instance.SpikeDataDensity(GlobalPosition, 50.0f);
                }
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
            if (_mesh != null && _mesh.MaterialOverride is StandardMaterial3D mat)
            {
                mat.AlbedoColor = _originalColor; // Restore visual
            }
            if (_collider != null)
            {
                _collider.Disabled = false; // Restore collision
            }
            GD.Print($"[Asset Condition]: Asset at {GlobalPosition} restored from ruin.");
        }

        if (Condition >= 50.0f && _smokeParticles != null)
        {
            _smokeParticles.Emitting = false;
        }
    }
}
