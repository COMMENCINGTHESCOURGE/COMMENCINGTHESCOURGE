using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Constraint Field — vinculum engine core.
/// Every value is a ratio, not an absolute.
/// This is the actual simulation. Player, NPC, and Zombie nodes read from here.
/// </summary>
public static class ConstraintField
{
    // ─── Player Needs (vinculums) ───
    public static float Hunger { get; set; } = 1.0f;        // food_available / requirement
    public static float Thirst { get; set; } = 1.0f;        // water_available / requirement
    public static float Fatigue { get; set; } = 0.0f;       // sleep_debt / threshold
    public static float Infection { get; set; } = 0.0f;     // infection_progression / terminal
    public static float Morale { get; set; } = 1.0f;        // social_fulfillment / need
    public static float Temperature { get; set; } = 0.5f;   // ambient_temp / comfortable_range

    // ─── World State ───
    public static int Day { get; set; } = -10;               // starts 10 days before breach
    public static float TimeOfDay { get; set; } = 6.0f;     // 0-24 hours
    public static string Weather { get; set; } = "clear";   // clear, rain, fog, storm
    public static string Season { get; set; } = "summer";   // summer, autumn, winter, spring
    public static float SurfaceTractionModifier { get; set; } = 1.0f; // Global slip multiplier

    // ─── Zombie Population ───
    public static int ZombieCount { get; set; } = 0;
    public static float HordeThreat { get; set; } = 0.0f;   // 0-1, escalates based on noise/light/blood

    // ─── Settlement ───
    public static float CommunityCareDensity { get; set; } = 0.5f; // the real survival metric
    public static int Population { get; set; } = 0;
    public static float GlassToWallRatio { get; set; } = 0.0f;

    // ─── Economy ───
    public static float CurrencyTrust { get; set; } = 1.0f; // Day -10 = 1.0, Day 8 = 0.0

    // ─── Degradation Rates ───
    private const float HungerRate = 0.002f;
    private const float ThirstRate = 0.003f;
    private const float FatigueRate = 0.001f;
    private const float InfectionRecovery = 0.0005f;

    // ─── Survival adaptation state ───
    private static float _survivalAdrenaline = 0f;    // Builds when needs are critical, enables short bursts
    private static float _metabolicAdaptation = 0f;   // Body adapts to scarcity over time
    private static float _timeSinceLastMeal = 0f;
    private static float _timeSinceLastDrink = 0f;
#pragma warning disable 0414
    private static bool _adrenalineSpike = false;
    private static bool _inKetosis = false;
#pragma warning restore 0414

    /// <summary>
    /// Called every physics tick.
    /// REVISED: Human survival is NOT a linear countdown.
    /// The body adapts. Adrenaline spikes under extreme stress.
    /// Metabolic pathways switch (ketosis). Everything is non-linear.
    /// </summary>
    public static void Tick(float delta)
    {
        // ─── Non-linear hunger/thirst model ───
        // In the flat model: Hunger -= 0.002f every tick -> dead in ~500 ticks.
        // In reality: the body SURGES when resources are absent, then adapts.
        //
        // Phase 1 (0-12h without food): normal, linear decline.
        // Phase 2 (12-72h): adrenaline builds, body scavenges glycogen stores,
        //   performance may actually INCREASE in short bursts.
        // Phase 3 (72h+): ketosis. Body burns fat. Metabolic rate drops 15-30%.
        //   Survival time EXTENDS, not accelerates.
        //
        // This model replaces the flat deficit approach.

        _timeSinceLastMeal += delta * 0.1f; // scale: 0.1 day per real second
        _timeSinceLastDrink += delta * 0.1f;

        // Forward calculation: Hunger = available / required
        // When you haven't eaten, the ratio drops — but NOT linearly.
        float hungerDrop;
        if (_timeSinceLastMeal < 0.5f) // <12h
        {
            hungerDrop = 0.002f; // Normal rate
            _inKetosis = false;
        }
        else if (_timeSinceLastMeal < 3.0f) // 12-72h
        {
            // Adrenaline phase: body SCROUNGES for energy.
            // Hunger drops slower as the body scavenges internal reserves.
            // Short bursts of performance increase are possible.
            hungerDrop = 0.001f; // Slower — body is fighting
            _survivalAdrenaline = Mathf.Clamp(_survivalAdrenaline + 0.003f * delta, 0f, 0.3f);
            _metabolicAdaptation = Mathf.Clamp(_metabolicAdaptation + 0.002f * delta, 0f, 0.5f);
            _inKetosis = false;
        }
        else // >72h: ketosis
        {
            // Body switches to fat-burning. Metabolic rate drops.
            // Survival extends. Adrenaline normalizes.
            hungerDrop = 0.0005f; // Much slower — adapted
            _inKetosis = true;
            _survivalAdrenaline = Mathf.Clamp(_survivalAdrenaline - 0.001f * delta, 0f, 0.3f);
            _metabolicAdaptation = Mathf.Clamp(_metabolicAdaptation + 0.001f * delta, 0f, 0.8f);
        }

        // Thirst is less adaptive — water is essential within ~3 days.
        // But the body does reduce output and concentrate urine.
        float thirstDrop;
        if (_timeSinceLastDrink < 1.0f) // <24h
            thirstDrop = 0.003f;
        else if (_timeSinceLastDrink < 2.0f) // 24-48h
            thirstDrop = 0.002f; // Body conserves
        else // >48h
            thirstDrop = 0.0015f; // Max conservation, still dangerous

        Hunger = Mathf.Clamp(Hunger - hungerDrop * delta, 0.0f, 1.0f);
        Thirst = Mathf.Clamp(Thirst - thirstDrop * delta, 0.0f, 1.0f);
        Fatigue = Mathf.Clamp(Fatigue + FatigueRate * delta, 0.0f, 1.0f);
        Temperature = Mathf.Clamp(Temperature + (Weather == "winter" ? -0.001f : 0.0005f) * delta, 0.0f, 1.0f);

        // Infection slowly recovers if below mild stage, progresses otherwise
        if (Infection > 0.25f)
            Infection = Mathf.Clamp(Infection + 0.001f * delta, 0.0f, 1.0f);
        else
            Infection = Mathf.Clamp(Infection - InfectionRecovery * delta, 0.0f, 1.0f);

        // Time
        TimeOfDay = (TimeOfDay + 0.1f * delta) % 24.0f;

        // Currency trust — Day 8 hard cutoff
        if (Day >= 8)
            CurrencyTrust = 0.0f;
        else if (Day >= -10)
            CurrencyTrust = Mathf.Max(0.0f, 1.0f - (Day + 10) * 0.055f);

        // Horde threat decays when no noise
        HordeThreat = Mathf.Clamp(HordeThreat - 0.002f * delta, 0.0f, 1.0f);

        // Morale from community
        Morale = Mathf.Clamp(CommunityCareDensity * 0.8f + 0.2f - Fatigue * 0.3f, 0.0f, 1.0f);
    }

    /// <summary>
    /// Add noise to the constraint field (from gunshots, vehicles, etc.)
    /// Attracts zombies proportional to noise level.
    /// </summary>
    public static void EmitNoise(float level)
    {
        HordeThreat = Mathf.Clamp(HordeThreat + level * 0.1f, 0.0f, 1.0f);
    }

    /// <summary>
    /// Player ate food.
    /// </summary>
    public static void ConsumeFood(float amount)
    {
        Hunger = Mathf.Clamp(Hunger + amount, 0.0f, 1.0f);
    }

    /// <summary>
    /// Player drank water.
    /// </summary>
    public static void ConsumeWater(float amount)
    {
        Thirst = Mathf.Clamp(Thirst + amount, 0.0f, 1.0f);
    }

    /// <summary>
    /// Player slept.
    /// </summary>
    public static void Sleep(float hours)
    {
        Fatigue = Mathf.Clamp(Fatigue - hours * 0.15f, 0.0f, 1.0f);
    }

    /// <summary>
    /// Export current constraint simulation state to standardized JSON.
    /// </summary>
    public static string SerializeState()
    {
        var state = new Godot.Collections.Dictionary
        {
            {"Hunger", Hunger},
            {"Thirst", Thirst},
            {"Fatigue", Fatigue},
            {"Infection", Infection},
            {"Morale", Morale},
            {"Temperature", Temperature},
            {"Day", Day},
            {"TimeOfDay", TimeOfDay},
            {"Weather", Weather},
            {"Season", Season},
            {"ZombieCount", ZombieCount},
            {"HordeThreat", HordeThreat},
            {"CommunityCareDensity", CommunityCareDensity},
            {"Population", Population},
            {"CurrencyTrust", CurrencyTrust},
            {"GlassToWallRatio", GlassToWallRatio}
        };
        return Godot.Json.Stringify(state);
    }
}
