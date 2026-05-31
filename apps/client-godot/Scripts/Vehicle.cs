using Godot;
using System;

/// <summary>
/// Phase 9: Custom Kinematic Vehicle (The Suspension Rig).
/// Extrapolates the Slide and Grip paradigm to a 4-wheeled rigid body.
/// Preserves Layer 1 snappiness (Patch xi) by avoiding spongy physics joints.
/// </summary>
public partial class Vehicle : CharacterBody3D
{
    // Steering constraints (The Angular Geometry)
    [Export] public float MaxSteerAngle = 0.5f;
    [Export] public float SteeringSpeed = 2.5f;

    // Phase 19: AI Controller Hooks
    public bool IsAIControlled = false;
    public float AiAccel = 0.0f;
    public float AiSteer = 0.0f;
    public bool AiHandbrake = false;

    // Node References
    [Export] public float EnginePower = 20.0f;
    [Export] public float MaxSpeed = 25.0f;
    [Export] public float SteeringAngle = 35.0f; // in degrees
    [Export] public float WheelBase = 2.5f;

    private float _currentSteering = 0.0f;
    private float _currentSpeed = 0.0f;
    private Random _rng = new Random();
    private RayCast3D _forwardScanner;

    public override void _Ready()
    {
        // Add a RayCast3D pointing forward and slightly down to read road integrity
        _forwardScanner = new RayCast3D();
        _forwardScanner.TargetPosition = new Vector3(0, -1.0f, -4.0f); // 4 meters ahead, pointing down
        _forwardScanner.Position = new Vector3(0, 0.5f, 0); // start slightly above ground
        AddChild(_forwardScanner);
        
        // Ensure StateRecorder exists (fallback instantiation for testing)
        if (StateRecorder.Instance == null)
        {
            var recorder = new StateRecorder();
            GetTree().Root.CallDeferred("add_child", recorder);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Phase 16: The Deterministic Timeline (State Recorder Integration)
        if (StateRecorder.Instance != null && StateRecorder.Instance.CurrentState == StateRecorder.RecorderState.Playback)
        {
            // Ignore physics inputs and override from the timeline
            if (StateRecorder.Instance.TryGetPlaybackTransform(this.Name, out Transform3D playbackTransform))
            {
                GlobalTransform = playbackTransform;
            }
            return; // Skip normal physics
        }

        float dt = (float)delta;
        
        // Input Handling
        float accel = 0f;
        float steer = 0f;
        bool handbrake = false;

        if (IsAIControlled)
        {
            accel = AiAccel;
            steer = AiSteer;
            handbrake = AiHandbrake;
        }
        else
        {
            accel = Input.GetActionStrength("ui_up") - Input.GetActionStrength("ui_down");
            steer = Input.GetActionStrength("ui_left") - Input.GetActionStrength("ui_right");
            handbrake = Input.IsKeyPressed(Key.Space); // Toe Stop Equivalent
        }

        // Engine acceleration
        if (accel != 0)
        {
            _currentSpeed = Mathf.MoveToward(_currentSpeed, accel * MaxSpeed, EnginePower * dt);
        }
        else
        {
            // Engine braking
            _currentSpeed = Mathf.MoveToward(_currentSpeed, 0, (EnginePower * 0.5f) * dt);
        }

        // Steering (Ackermann approximation via turning radius)
        _currentSteering = Mathf.Lerp(_currentSteering, steer * Mathf.DegToRad(SteeringAngle), 5.0f * dt);

        // Calculate intended movement vector
        Vector3 heading = -Transform.Basis.Z;
        
        // Applying turning
        if (Mathf.Abs(_currentSpeed) > 0.1f)
        {
            float turningRadius = WheelBase / Mathf.Sin(_currentSteering + 0.001f);
            float angularVelocity = _currentSpeed / turningRadius;
            RotateY(angularVelocity * dt);
            heading = -Transform.Basis.Z; // Update heading after rotation
        }

        Vector3 targetHorizontalVel = heading * _currentSpeed;
        Vector3 currentHorizontalVel = new Vector3(Velocity.X, 0, Velocity.Z);

        float traction = 1.0f * ConstraintField.SurfaceTractionModifier; // Cascade Engine: Weather affects base grip

        // Phase 11: Active Forward Scanning (RayCast)
        if (_forwardScanner.IsColliding())
        {
            Vector3 hitPos = _forwardScanner.GetCollisionPoint();
            Vector2 hitPos2D = new Vector2(hitPos.X, hitPos.Z);
            
            if (RoadNetwork.Instance != null)
            {
                var road = RoadNetwork.Instance.GetNearestRoad(hitPos2D, out float distToRoad);
                if (road != null && distToRoad < 2.5f && road.Integrity < 0.5f)
                {
                    traction = 0.4f; // Drop traction PREEMPTIVELY
                    RoadNetwork.Instance.RequestRoadRepair(hitPos2D); // The Pothole Program
                }
            }
        }

        // Phase 12: ASUS Hardware Gyroscope / Level Testing
        // The ASUS gyro (gravity vector) determines the real-world device tilt.
        Vector3 gravity = Input.GetGravity(); 
        
        // In Godot, a flat device has gravity pointing mostly down (-Y).
        // If the user physically tilts the device backward/forward drastically, gravity.Z spikes.
        // A spike indicates a severe drop or pothole simulation from the hardware proxy.
        if (Mathf.Abs(gravity.Z) > 6.0f) 
        {
            // The hardware proxy detected a steep un-level surface.
            traction = Mathf.Min(traction, 0.2f); // Break traction via physical tilt

            // Trigger local channel repair right under the car
            if (RoadNetwork.Instance != null)
            {
                RoadNetwork.Instance.RequestRoadRepair(new Vector2(GlobalPosition.X, GlobalPosition.Z));
            }
        }

        if (handbrake && currentHorizontalVel.Length() > 2.0f)
        {
            // Active Toe Stop (Handbrake drift)
            traction = Mathf.Min(traction, 0.05f); // Step-change in Mu
            _currentSpeed = Mathf.MoveToward(_currentSpeed, 0, EnginePower * 1.5f * dt); // shedding speed
            
            if (AmbientFXController.Instance != null)
            {
                AmbientFXController.Instance.TriggerSlideScrape(GlobalPosition, 3.0f); // Heavy smoke
            }
        }
        else if (currentHorizontalVel.Length() > 3.0f)
        {
            // Passive geometric slip angle calculation (tire traction limit)
            float dot = heading.Normalized().Dot(currentHorizontalVel.Normalized());
            
            // Step-change in Mu: If lateral slip > ~20 degrees (cos(20) = 0.94)
            if (dot < 0.94f)
            {
                traction = Mathf.Min(traction, 0.15f); // Friction drops, initiating the slide
                
                if (AmbientFXController.Instance != null && _rng.NextDouble() < 0.3)
                {
                    AmbientFXController.Instance.TriggerSlideScrape(GlobalPosition, 1.5f); // Tire smoke
                }
            }
        }

        // Apply traction
        Velocity = new Vector3(
            Mathf.Lerp(Velocity.X, targetHorizontalVel.X, 8.0f * traction * dt),
            Velocity.Y,
            Mathf.Lerp(Velocity.Z, targetHorizontalVel.Z, 8.0f * traction * dt)
        );

        // Gravity
        if (!IsOnFloor())
        {
            Velocity += new Vector3(0, -9.8f * dt, 0);
        }

        MoveAndSlide();

        // Phase 16: Record state if recording
        if (StateRecorder.Instance != null && StateRecorder.Instance.CurrentState == StateRecorder.RecorderState.Recording)
        {
            StateRecorder.Instance.RecordTransform(this.Name, GlobalTransform);
        }
    }
}
