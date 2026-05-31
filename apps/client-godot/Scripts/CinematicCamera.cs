using Godot;
using System;

/// <summary>
/// Phase 13: Virtual Production Emulator
/// A free-floating cinematic camera driven by the ASUS hardware gyroscope.
/// Provides hyper-realistic handheld camera shake for Machinima/filmmaking.
/// </summary>
public partial class CinematicCamera : Camera3D
{
    [Export] public float MovementSpeed = 10.0f;
    [Export] public float CameraWeight = 8.0f; // Lower = heavier/smoother, Higher = more jittery
    
    private Vector3 _targetRotation = Vector3.Zero;
    private Vector2 _panYaw = Vector2.Zero;

    public override void _Ready()
    {
        // Make this the active camera for the Director
        Current = true;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        
        // 1. Hardware Sensor Polling (ASUS Gyroscope)
        // gravity.X = left/right tilt (Roll)
        // gravity.Y = forward/backward tilt (Pitch)
        // gravity.Z = up/down (Gravity normal)
        Vector3 gravity = Input.GetGravity();
        
        // Calculate intended Pitch and Roll from physical hardware tilt
        float targetPitch = Mathf.Clamp(-gravity.Z * 0.1f, -1.5f, 1.5f);
        float targetRoll = Mathf.Clamp(gravity.X * 0.1f, -0.5f, 0.5f);

        // Allow manual Yaw (Pan) via joystick/keyboard
        float panInput = Input.GetActionStrength("ui_left") - Input.GetActionStrength("ui_right");
        _panYaw.X += panInput * 1.5f * dt;

        _targetRotation = new Vector3(targetPitch, _panYaw.X, targetRoll);

        // Lerp rotation to simulate the physical weight of a heavy cinematic rig
        Rotation = new Vector3(
            Mathf.Lerp(Rotation.X, _targetRotation.X, CameraWeight * dt),
            Mathf.Lerp(Rotation.Y, _targetRotation.Y, CameraWeight * dt),
            Mathf.Lerp(Rotation.Z, _targetRotation.Z, CameraWeight * dt)
        );

        // 2. Cinematic Dolly Movement (Floating in space)
        float forward = Input.GetActionStrength("ui_up") - Input.GetActionStrength("ui_down");
        Vector3 forwardVec = -GlobalTransform.Basis.Z;
        GlobalPosition += forwardVec * forward * MovementSpeed * dt;
    }
}
