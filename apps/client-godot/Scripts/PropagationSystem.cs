using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Sound, light, and scent propagation system.
/// 
/// Propagation model:
///   - Sound: radial emission with distance falloff, occluded by walls.
///     Measured in "noise units" (0-1). Gunshot = 0.9, whisper = 0.05.
///     Heard range: 0-50m depending on weather (rain masks, fog dampens).
///   
///   - Light: radial emission with distance falloff, occluded by opaque surfaces.
///     Measured in "lux" (0-1). Flashlight = 0.4, campfire = 0.7, flare = 0.9.
///     Visible range: 0-30m. Night multiplier: 1.5x (light stands out).
///   
///   - Scent/Blood: leaves a trail that decays over time. Wind-dependent.
///     Measured in "scent units" (0-1). Bleeding player = 0.3/tick.
///     Detection range: 0-20m. Wind carries scent downwind.
/// 
/// Zombie attraction is the SUM of sound + light + scent, clamped to [0,1].
/// Each zombie evaluates attraction against its personal threat/reward threshold.
/// </summary>
public partial class PropagationSystem : Node3D
{
    // ─── Event Types ───
    public enum PropagationType
    {
        Sound,
        Light,
        Scent
    }

    public class PropagationEvent
    {
        public PropagationType Type;
        public Vector3 Origin;
        public float Intensity;     // 0.0 - 1.0
        public float Radius;        // meters
        public float Duration;      // seconds (0 = instant)
        public float Elapsed;       // time since emission
        public int SourcePeerId;     // who made it (for multiplayer)
        public string SourceTag;     // "gunshot", "footstep", "campfire", etc.
        public float SporeConcentration;
        public float HumanPanic;
        public float DataDensity; // Phase 15: Observation pull

        public bool IsExpired => Elapsed >= Duration;
        public float CurrentIntensity => Mathf.Lerp(Intensity, 0f, Elapsed / Duration);
    }

    // ─── Active Events ───
    private List<PropagationEvent> _soundEvents = new();
    private List<PropagationEvent> _lightEvents = new();
    private List<PropagationEvent> _scentEvents = new();

    // ─── Configuration ───
    [ExportGroup("Propagation Ranges")]
    [Export] public float MaxSoundRange = 50f;
    [Export] public float MaxLightRange = 30f;
    [Export] public float MaxScentRange = 20f;

    [ExportGroup("Weather Modifiers")]
    [Export] public float RainSoundMask = 0.5f;   // Rain halves sound propagation
    [Export] public float RainLightDampen = 0.8f;  // Rain slightly dims light
    [Export] public float FogSoundDampen = 0.7f;   // Fog dampens sound
    [Export] public float FogLightDampen = 0.4f;   // Fog heavily dampens light
    [Export] public float WindScentCarry = 1.5f;   // Wind extends scent downwind
    [Export] public float NightLightMultiplier = 1.5f; // Light stands out more at night

    [ExportGroup("Grid Settings")]
    [Export] public float CellSize = 2.0f;   // For occlusion checks

    // ─── Static Instance for easy access ───
    public static PropagationSystem Instance { get; private set; }

    // ─── Weather Cache ───
    private string _lastWeather = "";
    private float _weatherModifierSound = 1.0f;
    private float _weatherModifierLight = 1.0f;
    private float _weatherScentDownwind = 1.0f;

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        UpdateWeatherModifiers();
        CleanExpiredEvents(_soundEvents, dt);
        CleanExpiredEvents(_lightEvents, dt);
        CleanExpiredEvents(_scentEvents, dt);
    }

    // ─── Emit Events ───
    /// <summary>
    /// Spikes the data density at a location, drawing sensors and observers.
    /// Phase 15: Kinematic Cascade
    /// </summary>
    public void SpikeDataDensity(Vector3 origin, float magnitude)
    {
        // Phase 15: Kinematic Cascade
        // We emit a scent-like persistent event that represents data density
        _scentEvents.Add(new PropagationEvent
        {
            Type = PropagationType.Scent, // Reusing scent for persistent invisible attraction
            Origin = origin,
            Intensity = 1.0f,
            Radius = magnitude, // Massive radius for observation pull
            Duration = 600f, // 10 minutes
            SourceTag = "data_density",
            DataDensity = magnitude
        });
        GD.Print($"Propagation: Data Density spike at {origin}. Observers converging.");
    }

    /// <summary>
    /// Emit a sound into the propagation field.
    /// </summary>
    public void EmitSound(Vector3 origin, float intensity, float duration, string tag = "noise", int peerId = -1)
    {
        float range = MaxSoundRange * intensity * GetWeatherSoundModifier();
        _soundEvents.Add(new PropagationEvent
        {
            Type = PropagationType.Sound,
            Origin = origin,
            Intensity = Mathf.Clamp(intensity, 0f, 1f),
            Radius = range,
            Duration = duration,
            SourceTag = tag,
            SourcePeerId = peerId
        });
    }

    /// <summary>
    /// Emit a light source.
    /// </summary>
    public void EmitLight(Vector3 origin, float intensity, float duration, string tag = "light", int peerId = -1)
    {
        float nightBonus = IsNight() ? NightLightMultiplier : 1.0f;
        float range = MaxLightRange * intensity * nightBonus * GetWeatherLightModifier();
        _lightEvents.Add(new PropagationEvent
        {
            Type = PropagationType.Light,
            Origin = origin,
            Intensity = Mathf.Clamp(intensity, 0f, 1f),
            Radius = range,
            Duration = duration,
            SourceTag = tag,
            SourcePeerId = peerId
        });
    }

    /// <summary>
    /// Emit scent/blood from a position. Persists longer than sound/light.
    /// </summary>
    public void EmitScent(Vector3 origin, float intensity, float duration, string tag = "scent", int peerId = -1)
    {
        float range = MaxScentRange * intensity;
        _scentEvents.Add(new PropagationEvent
        {
            Type = PropagationType.Scent,
            Origin = origin,
            Intensity = Mathf.Clamp(intensity, 0f, 1f),
            Radius = range,
            Duration = duration,
            SourceTag = tag,
            SourcePeerId = peerId
        });
    }

    // ─── Query Methods ───
    /// <summary>
    /// Get the total attraction value at a given position for a zombie.
    /// Returns (sound, light, scent, total) tuple.
    /// </summary>
    public (float sound, float light, float scent, float total) GetAttractionAt(Vector3 position, bool isZombie = true)
    {
        float sound = GetAggregatedValueAt(position, _soundEvents);
        float light = GetAggregatedValueAt(position, _lightEvents);
        float scent = GetAggregatedValueAt(position, _scentEvents);

        // Occlusion: check line-of-sight to nearest event source
        // Only apply occlusion if there's a clear path blocker
        sound = ApplyOcclusion(position, _soundEvents, sound);
        light = ApplyOcclusion(position, _lightEvents, light);

        float total = Mathf.Clamp(sound + light + scent, 0f, 1f);
        return (sound, light, scent, total);
    }



    /// <summary>
    /// Get the direction of strongest attraction for directional zombie AI.
    /// </summary>
    public Vector3 GetAttractionDirection(Vector3 from)
    {
        Vector3 dir = Vector3.Zero;
        float strongest = 0f;

        foreach (var evt in _soundEvents)
        {
            float val = GetValueAt(from, evt);
            if (val > strongest)
            {
                strongest = val;
                dir = (evt.Origin - from).Normalized();
            }
        }
        foreach (var evt in _lightEvents)
        {
            float val = GetValueAt(from, evt);
            if (val > strongest)
            {
                strongest = val;
                dir = (evt.Origin - from).Normalized();
            }
        }
        foreach (var evt in _scentEvents)
        {
            float val = GetValueAt(from, evt);
            if (val > strongest)
            {
                strongest = val;
                dir = (evt.Origin - from).Normalized();
            }
        }

        return strongest > 0.05f ? dir : Vector3.Zero;
    }

    /// <summary>
    /// Get the nearest significant event to a position.
    /// </summary>
    public PropagationEvent GetNearestEvent(Vector3 position, float minIntensity = 0.1f)
    {
        PropagationEvent nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var evt in GetAllActiveEvents())
        {
            float dist = position.DistanceTo(evt.Origin);
            float intensity = GetValueAt(position, evt);
            if (intensity >= minIntensity && dist < nearestDist)
            {
                nearestDist = dist;
                nearest = evt;
            }
        }
        return nearest;
    }

    /// <summary>
    /// Get all active events of a specific type.
    /// </summary>
    public List<PropagationEvent> GetEventsOfType(PropagationType type)
    {
        return type switch
        {
            PropagationType.Sound => _soundEvents,
            PropagationType.Light => _lightEvents,
            PropagationType.Scent => _scentEvents,
            _ => new List<PropagationEvent>()
        };
    }

    /// <summary>
    /// Get all active events.
    /// </summary>
    public IEnumerable<PropagationEvent> GetAllActiveEvents()
    {
        foreach (var e in _soundEvents) yield return e;
        foreach (var e in _lightEvents) yield return e;
        foreach (var e in _scentEvents) yield return e;
    }

    // ─── Internal Helpers ───
    private float GetValueAt(Vector3 position, PropagationEvent evt)
    {
        float dist = position.DistanceTo(evt.Origin);
        if (dist > evt.Radius) return 0f;

        // Linear falloff from origin
        float falloff = 1.0f - (dist / evt.Radius);
        float intensity = evt.CurrentIntensity * falloff;

        // Scent gets wind bonus (simplified: always active)
        if (evt.Type == PropagationType.Scent && dist > 5f)
            intensity *= _weatherScentDownwind;

        return Mathf.Clamp(intensity, 0f, 1f);
    }

    private float GetAggregatedValueAt(Vector3 position, List<PropagationEvent> events)
    {
        float total = 0f;
        foreach (var evt in events)
        {
            total += GetValueAt(position, evt);
        }
        return Mathf.Clamp(total, 0f, 1f);
    }

    private float ApplyOcclusion(Vector3 position, List<PropagationEvent> events, float currentValue)
    {
        if (currentValue <= 0f) return 0f;

        // Check if the nearest event of this type has line of sight
        PropagationEvent nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var evt in events)
        {
            float dist = position.DistanceTo(evt.Origin);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = evt;
            }
        }

        if (nearest == null) return currentValue;

        // Raycast for occlusion
        var space = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(position + Vector3.Up * 1.5f, nearest.Origin + Vector3.Up * 1.5f);
        query.CollisionMask = 1; // Default collision layer
        var result = space.IntersectRay(query);

        if (result.Count > 0)
        {
            // Something is in the way - attenuate
            return currentValue * 0.3f;
        }

        return currentValue;
    }

    private void CleanExpiredEvents(List<PropagationEvent> events, float dt)
    {
        for (int i = events.Count - 1; i >= 0; i--)
        {
            events[i].Elapsed += dt;
            if (events[i].IsExpired)
                events.RemoveAt(i);
        }
    }

    private void UpdateWeatherModifiers()
    {
        string currentWeather = ConstraintField.Weather;
        if (currentWeather == _lastWeather) return;
        _lastWeather = currentWeather;

        _weatherModifierSound = currentWeather switch
        {
            "rain" => RainSoundMask,
            "storm" => RainSoundMask * 0.7f,
            "fog" => FogSoundDampen,
            _ => 1.0f
        };

        _weatherModifierLight = currentWeather switch
        {
            "rain" => RainLightDampen,
            "storm" => RainLightDampen * 0.6f,
            "fog" => FogLightDampen,
            _ => 1.0f
        };

        // Wind always blows in a consistent direction (simplified: always active)
        _weatherScentDownwind = currentWeather switch
        {
            "storm" => 2.0f,
            "rain" => 1.2f,
            "fog" => 0.8f,
            _ => 1.0f
        };
    }

    private float GetWeatherSoundModifier()
    {
        return _weatherModifierSound;
    }

    private float GetWeatherLightModifier()
    {
        return _weatherModifierLight;
    }

    private bool IsNight()
    {
        float t = ConstraintField.TimeOfDay;
        return t < 5.0f || t > 20.0f;
    }

    // ─── Convenience Emission Presets ───
    /// <summary>
    /// Player fired a gun.
    /// </summary>
    public void Gunshot(Vector3 origin, float loudness = 0.9f)
    {
        EmitSound(origin, loudness, 1.5f, "gunshot");
        EmitLight(origin, 0.6f, 0.5f, "muzzle_flash");
    }

    /// <summary>
    /// Player fired a shotgun.
    /// </summary>
    public void ShotgunBlast(Vector3 origin)
    {
        EmitSound(origin, 1.0f, 2.0f, "shotgun");
        EmitLight(origin, 0.8f, 0.8f, "muzzle_flash");
    }

    /// <summary>
    /// Player walking or running.
    /// </summary>
    public void Footstep(Vector3 origin, bool running = false)
    {
        float intensity = running ? 0.2f : 0.05f;
        EmitSound(origin, intensity, 0.3f, running ? "running" : "footstep");
    }

    /// <summary>
    /// Melee weapon impact.
    /// </summary>
    public void MeleeHit(Vector3 origin)
    {
        EmitSound(origin, 0.15f, 0.5f, "melee_hit");
    }

    /// <summary>
    /// Zombie alert call (attracts other zombies).
    /// </summary>
    public void ZombieCall(Vector3 origin, float intensity = 0.4f)
    {
        EmitSound(origin, intensity, 2.0f, "zombie_call");
    }

    /// <summary>
    /// Player is bleeding - leaves scent trail.
    /// </summary>
    public void Bleeding(Vector3 origin, float severity = 0.3f)
    {
        EmitScent(origin, severity, 30.0f, "blood");
    }

    /// <summary>
    /// Campfire or torch.
    /// </summary>
    public void FireLight(Vector3 origin, float intensity = 0.7f)
    {
        EmitLight(origin, intensity, 3.0f, "fire");
        EmitScent(origin, 0.1f, 10.0f, "smoke");
    }

    /// <summary>
    /// Player turned on a flashlight.
    /// </summary>
    public void Flashlight(Vector3 origin, bool on)
    {
        if (on)
            EmitLight(origin, 0.4f, 0.5f, "flashlight"); // Repeated emission maintains it
    }

    /// <summary>
    /// Vehicle engine.
    /// </summary>
    public void VehicleEngine(Vector3 origin, float revs = 0.6f)
    {
        EmitSound(origin, revs, 1.0f, "engine");
    }

    /// <summary>
    /// Explosion.
    /// </summary>
    public void Explosion(Vector3 origin)
    {
        EmitSound(origin, 1.0f, 3.0f, "explosion");
        EmitLight(origin, 1.0f, 2.0f, "explosion_flash");
    }
}
