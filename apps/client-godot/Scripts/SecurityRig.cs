using Godot;

/// <summary>
/// Phase 23: The Security Rig (Prototyping / Distraction)
/// Symbiotic vehicle requiring a Pilot (Movement) and Operator (Distraction).
/// </summary>
public partial class SecurityRig : CharacterBody3D
{
    public static SecurityRig ActiveDistraction { get; private set; }

    public NPC Pilot { get; set; }
    public NPC Operator { get; set; }

    [Export] public float RigSpeed = 6.0f;
    [Export] public float TurnSpeed = 1.5f;
    
    public bool IsDistracting = false;

    private Vector3 _macroTarget;
    private float _distractionTimer = 0f;

    public override void _Ready()
    {
        AddToGroup("Vehicles");
        _macroTarget = GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        
        // Wait for symbiotic mounting
        if (Pilot == null) return;

        // --- Macro Control (Pilot) ---
        if (GlobalPosition.DistanceTo(_macroTarget) < 3.0f)
        {
            // Pilot secures perimeter by patrolling in large arcs
            float angle = (float)GD.RandRange(0, Mathf.Pi * 2);
            _macroTarget = GlobalPosition + new Vector3(Mathf.Cos(angle) * 40f, 0, Mathf.Sin(angle) * 40f);
        }

        Vector3 direction = (_macroTarget - GlobalPosition).Normalized();
        Velocity = direction * RigSpeed;

        if (ConstraintField.SurfaceTractionModifier < 0.5f)
        {
            Velocity *= 0.6f; // Heavy rig struggles in snow
        }

        MoveAndSlide();

        if (Velocity.LengthSquared() > 0.1f)
        {
            float targetAngle = Mathf.Atan2(-Velocity.X, -Velocity.Z);
            Rotation = new Vector3(0, Mathf.LerpAngle(Rotation.Y, targetAngle, TurnSpeed * dt), 0);
        }

        // --- Micro Control (Operator) ---
        if (Operator != null)
        {
            _distractionTimer -= dt;
            if (_distractionTimer <= 0)
            {
                // The Operator deploys the Distraction Tactic
                IsDistracting = true;
                ActiveDistraction = this;
                
                GD.Print($"[Security Rig]: Operator deployed distraction flare! Pulling Scourge aggro.");
                
                if (WorldMemoryField.Instance != null)
                {
                    // Massive cinematic spike to ensure drones film the zombie swarm hitting the rig
                    WorldMemoryField.Instance.SpikeDataDensity(GlobalPosition, 600f); 
                }

                _distractionTimer = 4.0f; // Modulate frequency
            }
        }
        else
        {
            IsDistracting = false;
            if (ActiveDistraction == this) ActiveDistraction = null;
        }
    }
}
