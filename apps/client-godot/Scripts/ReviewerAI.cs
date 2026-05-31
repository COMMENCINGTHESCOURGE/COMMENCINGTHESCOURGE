using Godot;

/// <summary>
/// Phase 19: The Reviewer AI (Kinetic Feeders)
/// Drives the Vehicle aggressively to generate B-Roll events.
/// </summary>
public partial class ReviewerAI : Node
{
    private Vehicle _vehicle;
    private double _stateTimer = 0;
    
    private enum DriveState
    {
        Accelerate,
        Drift,
        Recover
    }
    
    private DriveState _currentState = DriveState.Accelerate;

    public override void _Ready()
    {
        _vehicle = GetParentOrNull<Vehicle>();
        if (_vehicle != null)
        {
            _vehicle.IsAIControlled = true;
            _stateTimer = GD.RandRange(2.0, 5.0); // Random initial acceleration time
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_vehicle == null) return;
        
        // If the Timeline is in playback, we stop issuing AI commands so the replay is exact.
        if (StateRecorder.Instance != null && StateRecorder.Instance.CurrentState == StateRecorder.RecorderState.Playback)
        {
            return;
        }

        _stateTimer -= delta;

        switch (_currentState)
        {
            case DriveState.Accelerate:
                _vehicle.AiAccel = 1.0f;
                _vehicle.AiSteer = 0.0f;
                _vehicle.AiHandbrake = false;

                if (_stateTimer <= 0)
                {
                    _currentState = DriveState.Drift;
                    _stateTimer = GD.RandRange(1.0, 2.5); // Hold drift
                    
                    // Phase 19: Emit Data Density and Dialogue
                    if (PropagationSystem.Instance != null)
                    {
                        PropagationSystem.Instance.SpikeDataDensity(_vehicle.GlobalPosition, 300.0f);
                        GD.Print($"[Reviewer AI in {_vehicle.Name}]: Traction control is completely ignoring the lateral load!");
                    }
                }
                break;

            case DriveState.Drift:
                // Floor it while ripping the handbrake and throwing the wheel
                _vehicle.AiAccel = 1.0f;
                _vehicle.AiSteer = 1.0f; // Hard right (or could be random)
                _vehicle.AiHandbrake = true;

                if (_stateTimer <= 0)
                {
                    _currentState = DriveState.Recover;
                    _stateTimer = GD.RandRange(1.0, 2.0); // Recovery time
                }
                break;

            case DriveState.Recover:
                // Straighten out, off the brake
                _vehicle.AiAccel = 0.5f;
                _vehicle.AiSteer = -0.5f; // Counter-steer
                _vehicle.AiHandbrake = false;

                if (_stateTimer <= 0)
                {
                    _currentState = DriveState.Accelerate;
                    _stateTimer = GD.RandRange(4.0, 8.0);
                }
                break;
        }
    }
}
