using Godot;

/// <summary>
/// Phase 18: Automated B-Roll (Observation Layer)
/// Static camera that actively polls the PropagationSystem for high tension/data density
/// and automatically pans to frame the event.
/// </summary>
public partial class BRollCamera : Camera3D
{
    [Export] public float PanSpeed = 2.0f;
    [Export] public float DetectionRadius = 100.0f;

    private Vector3 _defaultRotation;
    private Vector3 _targetLookPosition;
    private bool _hasTarget = false;

    public override void _Ready()
    {
        _defaultRotation = Rotation;
        AddToGroup("BRollCameras");
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        
        if (PropagationSystem.Instance != null)
        {
            // Hunt for Data Density (encoded as Scent in our Phase 15 implementation)
            // or high sound/panic events
            var nearestEvent = PropagationSystem.Instance.GetNearestEvent(GlobalPosition, 0.2f);
            
            if (nearestEvent != null && GlobalPosition.DistanceTo(nearestEvent.Origin) <= DetectionRadius)
            {
                _targetLookPosition = nearestEvent.Origin;
                _hasTarget = true;
            }
            else
            {
                _hasTarget = false;
            }
        }

        // Apply cinematic panning
        if (_hasTarget)
        {
            // Create a temporary transform looking at the target
            var currentTransform = GlobalTransform;
            var targetTransform = currentTransform.LookingAt(_targetLookPosition, Vector3.Up);
            
            // Spherical interpolation for smooth camera panning
            GlobalTransform = currentTransform.InterpolateWith(targetTransform, PanSpeed * dt);
        }
        else
        {
            // Slowly return to default forward-facing rotation
            Rotation = new Vector3(
                Mathf.LerpAngle(Rotation.X, _defaultRotation.X, PanSpeed * 0.5f * dt),
                Mathf.LerpAngle(Rotation.Y, _defaultRotation.Y, PanSpeed * 0.5f * dt),
                Mathf.LerpAngle(Rotation.Z, _defaultRotation.Z, PanSpeed * 0.5f * dt)
            );
        }
    }
}
