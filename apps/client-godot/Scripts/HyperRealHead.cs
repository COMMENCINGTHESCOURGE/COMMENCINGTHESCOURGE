using Godot;

/// <summary>
/// Phase 26: The Hyper-Real Proxemics
/// Micro-animation controller for high-LOD NPC heads in the Intimate Proximity Zone.
/// Simulates blinking via scale manipulation and procedural eye tracking with saccades.
/// </summary>
public partial class HyperRealHead : Node3D
{
    [ExportGroup("Mesh References")]
    [Export] public Node3D EyeL;
    [Export] public Node3D EyeR;
    [Export] public Node3D EyelidL;
    [Export] public Node3D EyelidR;

    // Blink State
    private float _blinkTimer = 0f;
    private bool _isBlinking = false;
    private float _nextBlinkTime = 3.0f;
    
    // Eye Tracking State
    private Vector3 _lookTarget;
    private Vector3 _saccadeOffset;
    private float _saccadeTimer = 0f;
    private bool _isTracking = false;

    public override void _Ready()
    {
        _nextBlinkTime = (float)GD.RandRange(2.0, 6.0);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        UpdateBlink(dt);
        
        if (_isTracking && EyeL != null && EyeR != null)
        {
            UpdateSaccades(dt);
            
            // Apply looking logic. 
            // In Godot, LookAt expects global coordinates.
            Vector3 finalTarget = _lookTarget + _saccadeOffset;
            
            // Smoothly look at the target (placeholder for immediate LookAt)
            // Realistically we'd slerp the quaternions, but LookAt is fine for the baseline
            EyeL.LookAt(finalTarget, Vector3.Up);
            EyeR.LookAt(finalTarget, Vector3.Up);
        }
    }

    private void UpdateBlink(float dt)
    {
        if (EyelidL == null || EyelidR == null) return;

        _blinkTimer += dt;
        
        if (!_isBlinking && _blinkTimer > _nextBlinkTime)
        {
            _isBlinking = true;
            _blinkTimer = 0f;
            _nextBlinkTime = (float)GD.RandRange(1.5, 5.0); // Next blink interval
        }

        if (_isBlinking)
        {
            // A blink takes roughly 0.15 seconds
            float progress = Mathf.Min(1.0f, _blinkTimer / 0.15f);
            
            // Parabola: 1.0 down to 0.1, then back up to 1.0
            float scaleY;
            if (progress < 0.5f)
            {
                scaleY = Mathf.Lerp(1.0f, 0.1f, progress * 2.0f); // Closing
            }
            else
            {
                scaleY = Mathf.Lerp(0.1f, 1.0f, (progress - 0.5f) * 2.0f); // Opening
            }
            
            // Apply scale to simulate eyelid closing
            EyelidL.Scale = new Vector3(EyelidL.Scale.X, scaleY, EyelidL.Scale.Z);
            EyelidR.Scale = new Vector3(EyelidR.Scale.X, scaleY, EyelidR.Scale.Z);

            if (progress >= 1.0f)
            {
                _isBlinking = false;
                _blinkTimer = 0f;
                // Ensure fully open
                EyelidL.Scale = new Vector3(EyelidL.Scale.X, 1.0f, EyelidL.Scale.Z);
                EyelidR.Scale = new Vector3(EyelidR.Scale.X, 1.0f, EyelidR.Scale.Z);
            }
        }
    }

    private void UpdateSaccades(float dt)
    {
        _saccadeTimer -= dt;
        if (_saccadeTimer <= 0)
        {
            // Human eyes dart around slightly during conversation
            _saccadeOffset = new Vector3(
                (float)GD.RandRange(-0.05, 0.05),
                (float)GD.RandRange(-0.05, 0.05),
                (float)GD.RandRange(-0.05, 0.05)
            );
            _saccadeTimer = (float)GD.RandRange(0.2, 1.5); // Micro-movement intervals
        }
    }

    /// <summary>
    /// Call this from NPC.cs when in Intimate or Conversation zone.
    /// </summary>
    public void SetTrackingTarget(Vector3 targetGlobalPos)
    {
        _isTracking = true;
        _lookTarget = targetGlobalPos;
    }

    /// <summary>
    /// Call this when the player leaves the conversation zones.
    /// </summary>
    public void StopTracking()
    {
        _isTracking = false;
        
        // Reset eyes to forward
        if (EyeL != null && EyeR != null)
        {
            Vector3 forwardTarget = GlobalPosition + GlobalTransform.Basis.Z * 5.0f;
            EyeL.LookAt(forwardTarget, Vector3.Up);
            EyeR.LookAt(forwardTarget, Vector3.Up);
        }
    }
}
