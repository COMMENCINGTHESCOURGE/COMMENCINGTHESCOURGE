using Godot;
using System;

/// <summary>
/// Player controller. Reads from ConstraintField for needs.
/// WASD movement, needs degradation, infection effects.
/// </summary>
public partial class Player : CharacterBody3D
{
    [Export] public float Speed = 5.0f;
    [Export] public float SprintMultiplier = 1.6f;
    [Export] public float JumpForce = 8.0f;
    [Export] public float MouseSensitivity = 0.002f;

    private Node3D _head;
    private Camera3D _camera;
    private float _cameraPitch = 0.0f;

    // Speed modifiers from constraint state
    private float _speedModifier = 1.0f;

    // Hyperrealistic Procedural Visual Physics (Layer 2 Foreground)
    private float _bobTimer = 0.0f;
    private const float BobFrequencyWalk = 12.0f;
    private const float BobFrequencySprint = 16.0f;
    private const float BobAmplitudeWalk = 0.035f;
    private const float BobAmplitudeSprint = 0.08f;

    private float _targetTilt = 0.0f;
    private float _currentTilt = 0.0f;
    private const float TiltSpeed = 8.0f;

    private float _landCompression = 0.0f;
    private float _verticalVelocityLastFrame = 0.0f;
    private bool _wasOnFloorLastFrame = true;
    private Random _rng = new Random();

    public override void _Ready()
    {
        _head = GetNode<Node3D>("Head");
        _camera = _head.GetNode<Camera3D>("Camera3D");
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Input(InputEvent @event)
    {
        // Mouse look (snappy and precise mechanical input)
        if (@event is InputEventMouseMotion mouse)
        {
            RotateY(-mouse.Relative.X * MouseSensitivity);
            _cameraPitch = Mathf.Clamp(_cameraPitch - mouse.Relative.Y * MouseSensitivity, -90.0f, 90.0f);
            _head.RotationDegrees = new Vector3(_cameraPitch, 0, 0);
        }
    }

    private bool _wasMoving = false;
    private float _footstepTimer = 0f;

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Tick the constraint field once per frame
        ConstraintField.Tick(dt);

        // Speed modifier from constraint state
        // REVISED: Adrenaline burst when starving can TEMPORARILY SURPASS normal speed.
        // The body in survival mode can out-perform a fed body for short windows.
        // This replaces the pure-penalty model where "hungry = slow."
        _speedModifier = 1.0f;

        // Adrenaline bonus: if the body has been adapting to scarcity,
        // short bursts of speed are possible (hunting, escaping).
        // This is NOT a bonus that stacks — it's a temporary surge that fades.
        float adrenalineBonus = 0f;
        if (ConstraintField.Hunger < 0.3f)
        {
            // In phase 2 (adrenaline), body can surge ~15% faster briefly
            adrenalineBonus = 0.15f;
        }

        // Penalties still apply, but adrenaline offsets them
        if (ConstraintField.Hunger < 0.1f) _speedModifier *= 0.85f; // Less penalty than before (0.7 -> 0.85)
        if (ConstraintField.Thirst < 0.2f) _speedModifier *= 0.7f;
        if (ConstraintField.Fatigue > 0.8f) _speedModifier *= 0.6f;
        if (ConstraintField.Infection > 0.5f) _speedModifier *= 0.5f;
        if (ConstraintField.Temperature < 0.15f) _speedModifier *= 0.8f;

        // Apply adrenaline as a temporary override (not a permanent buff)
        // isMoving and inputDir are declared below — we'll check movement state directly
        Vector2 checkDir = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        if (adrenalineBonus > 0f && checkDir.Length() > 0.1f)
        {
            _speedModifier = Mathf.Max(_speedModifier, 1.0f + adrenalineBonus);
        }

        // Input
        Vector2 inputDir = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

        bool sprinting = Input.IsActionPressed("ui_accept") && ConstraintField.Fatigue < 0.9f;
        bool isMoving = direction.Length() > 0.1f;

        // Jump (Snappy 2D mechanical inheritance)
        if (IsOnFloor() && Input.IsActionJustPressed("ui_accept"))
        {
            Velocity = new Vector3(Velocity.X, JumpForce, Velocity.Z);
        }

        // Precise Movement Physics (Layer 1 snappiness)
        float currentSpeed = Speed * _speedModifier * (sprinting ? SprintMultiplier : 1.0f);
        
        // --- Ghost Braid: Slide and Grip Paradigm (Patch xv) ---
        Vector3 targetHorizontalVel = direction * currentSpeed;
        Vector3 currentHorizontalVel = new Vector3(Velocity.X, 0, Velocity.Z);
        
        float traction = 1.0f; // Baseline rolling grip
        bool isPivoting = Input.IsKeyPressed(Key.Shift); // Active Truck Pivot (Camber Thrust)

        if (isPivoting && currentHorizontalVel.Length() > 1.5f)
        {
            // The player is intentionally rolling the ankle/truck to break traction
            traction = 0.05f; 
            
            float dot = direction.Normalized().Dot(currentHorizontalVel.Normalized());
            
            // The Toe Stop: If sliding and heavily pulling backwards against momentum
            if (dot < -0.5f)
            {
                // Massive step-change in friction: violently arrest momentum
                traction = 4.0f; 
                
                if (AmbientFXController.Instance != null && _footstepTimer <= 0.1f)
                {
                    AmbientFXController.Instance.TriggerSlideScrape(GlobalPosition, 3.0f); // High intensity burst
                }
            }
            else
            {
                // Smooth power slide
                if (AmbientFXController.Instance != null && _rng.NextDouble() < 0.2) // continuous trail
                {
                    AmbientFXController.Instance.TriggerSlideScrape(GlobalPosition, 1.5f);
                }
            }
        }
        else if (currentHorizontalVel.Length() > 1.0f && isMoving)
        {
            // Passive geometric slip angle calculation
            float dot = direction.Normalized().Dot(currentHorizontalVel.Normalized());
            
            // Step-change in Mu: If approach angle > ~25 degrees (cos(25) = 0.9)
            if (dot < 0.90f)
            {
                traction = 0.2f; // Friction drops, initiating the slide
                
                // Trigger visual/audio extrusion of the mechanical state
                if (AmbientFXController.Instance != null && _footstepTimer <= 0.1f)
                {
                    AmbientFXController.Instance.TriggerSlideScrape(GlobalPosition, 1.0f);
                }
            }
        }
        else if (!isMoving)
        {
            traction = 0.8f; // Deceleration braking friction
        }

        Velocity = new Vector3(
            Mathf.Lerp(Velocity.X, targetHorizontalVel.X, 12.0f * traction * dt),
            Velocity.Y,
            Mathf.Lerp(Velocity.Z, targetHorizontalVel.Z, 12.0f * traction * dt)
        );

        // Sprint fatigue cost
        if (sprinting && isMoving)
            ConstraintField.Fatigue = Mathf.Clamp(ConstraintField.Fatigue + 0.002f * dt, 0.0f, 1.0f);

        // Kinetic Tracking: Land compression trigger
        bool onFloor = IsOnFloor();
        if (onFloor && !_wasOnFloorLastFrame)
        {
            float fallSpeed = Mathf.Abs(_verticalVelocityLastFrame);
            if (fallSpeed > 1.5f)
            {
                // Procedural camera compression on impact proportional to velocity
                _landCompression = Mathf.Min(0.35f, fallSpeed * 0.025f);
            }
        }
        _wasOnFloorLastFrame = onFloor;
        _verticalVelocityLastFrame = Velocity.Y;

        // Decay the impact compression offset back to baseline
        _landCompression = Mathf.Lerp(_landCompression, 0.0f, 12.0f * dt);

        // Move the CharacterBody3D physics body
        MoveAndSlide();

        // ─── Procedural Visual Physics (Layer 2 Foreground FX) ───
        if (isMoving && IsOnFloor())
        {
            float frequency = sprinting ? BobFrequencySprint : BobFrequencyWalk;
            float amplitude = sprinting ? BobAmplitudeSprint : BobAmplitudeWalk;
            _bobTimer += frequency * dt;

            // Physical lateral and vertical camera bobbing
            float bobY = Mathf.Sin(_bobTimer) * amplitude;
            float bobX = Mathf.Cos(_bobTimer * 0.5f) * amplitude * 0.4f;

            // Apply offsets (injecting impact compression on Y axis)
            _camera.Position = new Vector3(bobX, bobY - _landCompression, 0.0f);

            // Strafe leaning: roll camera slightly in opposite direction of movement
            _targetTilt = -inputDir.X * (sprinting ? 0.035f : 0.015f);
            
            // Exaggerate tilt if actively pivoting (truck tilt)
            if (Input.IsKeyPressed(Key.Shift) && currentHorizontalVel.Length() > 1.5f)
            {
                _targetTilt = -inputDir.X * 0.15f;
            }
        }
        else
        {
            _bobTimer = 0.0f;
            // Smoothly return to center
            _camera.Position = new Vector3(
                Mathf.Lerp(_camera.Position.X, 0.0f, 10.0f * dt),
                Mathf.Lerp(_camera.Position.Y, -_landCompression, 10.0f * dt),
                Mathf.Lerp(_camera.Position.Z, 0.0f, 10.0f * dt)
            );
            _targetTilt = 0.0f;
        }

        // Apply roll/tilt interpolation smoothly
        _currentTilt = Mathf.Lerp(_currentTilt, _targetTilt, TiltSpeed * dt);
        _camera.Rotation = new Vector3(0.0f, 0.0f, _currentTilt);

        // ─── Propagation: footsteps ───
        if (isMoving && PropagationSystem.Instance != null)
        {
            _footstepTimer -= dt;
            float footstepInterval = sprinting ? 0.3f : 0.6f;
            if (_footstepTimer <= 0)
            {
                PropagationSystem.Instance.Footstep(GlobalPosition, sprinting);
                _footstepTimer = footstepInterval;
            }
        }

        // ─── Propagation: bleeding from infection ───
        if (ConstraintField.Infection > 0.4f && PropagationSystem.Instance != null)
        {
            float severity = Mathf.Lerp(0.05f, 0.3f, ConstraintField.Infection);
            PropagationSystem.Instance.Bleeding(GlobalPosition, severity);
        }
    }
}
