using Godot;
using System;

/// <summary>
/// Zombie AI — Utility-based decision making.
/// Reads zombie tier from pre-collapse conditioning vinculums.
/// </summary>
public partial class Zombie : CharacterBody3D
{
    // ─── Zombie Tier — derived from pre-collapse physical conditioning ───
    public enum ZombieTier
    {
        Walker,     // physical_condition/degeneration = 0.30 (sedentary)
        Runner,     // 0.50 (moderate activity)
        Brute,      // 0.85 (physically active)
        Spitter,    // 0.40 (ranged variant)
        Crawler,    // 0.25 (low profile)
        Mutant      // 0.95+ (extreme survival stress — boss)
    }

    public enum ZombieSocialProfile
    {
        Wealthy,       // Soft life -> rapid decay, low damage
        MiddleClass,   // Baseline
        WorkingClass,  // Physical labor -> slower decay, high damage
        Homeless       // Extreme environmental adaptation -> lowest decay, max persistence and damage
    }

    [Export] public ZombieTier Tier = ZombieTier.Walker;
    [Export] public ZombieSocialProfile SocialProfile = ZombieSocialProfile.MiddleClass;
    [Export] public float SightRange = 20.0f;
    [Export] public float HearingRange = 30.0f;
    [Export] public float BaseSpeed = 2.0f;
    [Export] public float Health = 100.0f;

    private Node3D _player;
    private float _stamina = 100.0f;
    private Random _rng = new();
    private Vector3 _wanderTarget;
    private float _wanderTimer = 0.0f;

    // Tier-specific stats
    private float _speedMultiplier = 1.0f;
    private float _healthMultiplier = 1.0f;
    private float _impactMultiplier = 1.0f;
    private bool _canClimb = false;
    private bool _isRanged = false;

    public override void _Ready()
    {
        _player = (Node3D)GetTree().GetFirstNodeInGroup("Player");
        ApplyTierStats();
        SetNewWanderTarget();
    }

    // ─── Decomposition state ───
    private float _daysSinceDeath = 0f;
    private float _decompositionRate = 1f; // multiplier, set by tier

    private void ApplyTierStats()
    {
        // REVISED: Pre-collapse physical conditioning determines how the body
        // DEGRADES, not how strong it is. A body that was never stressed without
        // infrastructure falls apart rapidly. A body that was already dense
        // and scarred persists.
        //
        // Tier = pre-collapse infrastructure exposure history:
        //   Walker    = high physical stress, low infrastructure access = PERSISTS
        //   Brute     = high gym conditioning, high infrastructure access = COLLAPSES FAST
        //   Runner    = moderate activity, average access = moderate
        //   Crawler   = extreme survival stress, ultra-low access = hardest to kill
        //   Spitter   = unique pathology, unpredictable
        //   Mutant    = extreme+unique = boss tier
        switch (Tier)
        {
            case ZombieTier.Walker:
                _speedMultiplier = 0.5f;     // Slow but relentless
                _healthMultiplier = 1.5f;    // 1.5x — body was already dense
                _decompositionRate = 0.3f;   // Decomposes 70% slower than baseline
                _canClimb = false;
                break;
            case ZombieTier.Runner:
                _speedMultiplier = 1.8f;
                _healthMultiplier = 0.6f;    // Moderate conditioning, moderate persistence
                _decompositionRate = 0.7f;
                _canClimb = true;
                break;
            case ZombieTier.Brute:
                _speedMultiplier = 0.9f;     // Actually fast at first (muscle mass)
                _healthMultiplier = 1.8f;    // Starts strong
                _decompositionRate = 2.5f;   // Decomposes 250% faster — collapses from within
                _canClimb = false;
                break;
            case ZombieTier.Spitter:
                _speedMultiplier = 0.7f;
                _healthMultiplier = 0.7f;
                _decompositionRate = 0.6f;
                _isRanged = true;
                break;
            case ZombieTier.Crawler:
                _speedMultiplier = 1.0f;     // Not fast, not slow
                _healthMultiplier = 0.8f;    // Low starting HP...
                _decompositionRate = 0.1f;   // ...but barely decomposes at all
                _canClimb = true;
                break;
            case ZombieTier.Mutant:
                _speedMultiplier = 1.3f;
                _healthMultiplier = 3.0f;    // Boss stats
                _decompositionRate = 0.05f;  // Nearly permanent
                _canClimb = true;
                break;
        }

        // Apply social profile modifiers (Class-Based Strength Inversion)
        switch (SocialProfile)
        {
            case ZombieSocialProfile.Wealthy:
                _decompositionRate *= 2.0f;  // Decays twice as fast
                _impactMultiplier = 0.6f;    // Weak attacks
                break;
            case ZombieSocialProfile.MiddleClass:
                _impactMultiplier = 1.0f;
                break;
            case ZombieSocialProfile.WorkingClass:
                _decompositionRate *= 0.6f;  // Slower decay
                _impactMultiplier = 1.4f;    // Stronger attacks
                break;
            case ZombieSocialProfile.Homeless:
                _decompositionRate *= 0.2f;  // Barely decays
                _impactMultiplier = 2.0f;    // Maximum damage
                break;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_player == null) return;

        float distance = GlobalPosition.DistanceTo(_player.GlobalPosition);
        bool canSee = distance < SightRange && IsLineOfSightClear();

        // ─── Propagation system integration ───
        // Query the propagation field for sound/light/scent at zombie position
        float soundAttraction, lightAttraction, scentAttraction, totalAttraction;
        if (PropagationSystem.Instance != null)
        {
            (soundAttraction, lightAttraction, scentAttraction, totalAttraction) =
                PropagationSystem.Instance.GetAttractionAt(GlobalPosition);
        }
        else
        {
            soundAttraction = 0f;
            lightAttraction = 0f;
            scentAttraction = 0f;
            totalAttraction = 0f;
        }

        // Utility AI scoring — threat vs reward
        float attackScore = canSee ? 0.9f : 0.1f;

        // Investigate score: when propagation events attract the zombie
        // Sound > 0.2 means something made a noise nearby
        // Light at night means prey is visible
        // Scent means wounded prey is nearby
        float propagationScore = 0f;
        if (totalAttraction > 0.15f) propagationScore = 0.5f + totalAttraction * 0.4f;

        float investigateScore = (!canSee && (distance < HearingRange || propagationScore > 0)) ? 
            Mathf.Max(0.7f, propagationScore) : 0.0f;

        float fleeScore = (_stamina < 15.0f || Health < 20.0f) ? 0.8f : 0.0f;
        float wanderScore = 0.3f;

        // Horde attraction: if ConstraintField horde threat is high, bias toward attack
        if (ConstraintField.HordeThreat > 0.7f)
            attackScore = Mathf.Clamp(attackScore + 0.3f, 0.0f, 1.0f);

        // Night bonus: zombies are more aggressive and sensitive to light/sound
        bool isNight = ConstraintField.TimeOfDay < 5.0f || ConstraintField.TimeOfDay > 20.0f;
        if (isNight)
        {
            investigateScore += 0.2f;
            attackScore += 0.1f;
        }

        float maxScore = Mathf.Max(attackScore, Mathf.Max(investigateScore, Mathf.Max(fleeScore, wanderScore)));

        // Stamina recovery
        _stamina = Mathf.Clamp(_stamina + 0.1f * dt, 0.0f, 100.0f);

        if (maxScore == attackScore && canSee)
        {
            Chase(dt);
        }
        else if (maxScore == investigateScore)
        {
            Investigate(dt);
        }
        else if (maxScore == fleeScore)
        {
            Retreat(dt);
        }
        else
        {
            Wander(dt);
        }

        // If scent is strong and zombie is investigating, share the find with nearby zombies
        if (scentAttraction > 0.3f && maxScore == investigateScore)
        {
            PropagationSystem.Instance?.ZombieCall(GlobalPosition, 0.3f);
        }

        // ─── DECOMPOSITION: Bodies decay over time ───
        // Each in-game day, zombies lose HP proportional to their decomposition rate.
        // A Brute (decompositionRate=2.5) loses ~2.5 HP per day.
        // A Crawler (decompositionRate=0.1) loses ~0.1 HP per day — virtual immortality.
        // This means the Brute that starts strong (1.8x HP) will be below baseline
        // within a week, while the Walker keeps going for months.
        _daysSinceDeath += dt * 0.1f; // 0.1 day per real second; scale to taste
        float baseHealth = _healthMultiplier * 100f;
        float decompositionLoss = _decompositionRate * _daysSinceDeath * 0.5f;
        Health = Mathf.Max(1f, baseHealth - decompositionLoss);

        // Visual indicator: slow down as decomposition progresses
        if (Health < baseHealth * 0.3f)
            _speedMultiplier *= 0.7f;
    }

    private bool IsLineOfSightClear()
    {
        var space = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(GlobalPosition + Vector3.Up, _player.GlobalPosition + Vector3.Up);
        var result = space.IntersectRay(query);
        return result.Count == 0 || (Node3D)result["collider"] == _player;
    }

    private void Chase(double delta)
    {
        float speed = BaseSpeed * _speedMultiplier;
        if (ConstraintField.TimeOfDay < 6.0f || ConstraintField.TimeOfDay > 20.0f)
            speed *= 1.2f; // Faster at night
        MoveTowards(_player.GlobalPosition, delta, speed);
        _stamina -= 2.0f * (float)delta;

        // Emit noise when attacking
        if (GlobalPosition.DistanceTo(_player.GlobalPosition) < 3.0f)
            ConstraintField.EmitNoise(0.3f);
    }

    private void Investigate(double delta)
    {
        // Move toward strongest propagation source
        Vector3 target;
        if (PropagationSystem.Instance != null)
        {
            Vector3 dir = PropagationSystem.Instance.GetAttractionDirection(GlobalPosition);
            if (dir.Length() > 0.1f)
                target = GlobalPosition + dir * 15.0f;
            else
                target = _player.GlobalPosition;
        }
        else
        {
            target = _player.GlobalPosition;
        }
        MoveTowards(target, delta, BaseSpeed * _speedMultiplier * 0.7f);
    }

    private void Retreat(double delta)
    {
        Vector3 away = GlobalPosition - (_player.GlobalPosition - GlobalPosition).Normalized() * 15.0f;
        MoveTowards(away, delta, BaseSpeed * _speedMultiplier * 0.8f);
    }

    private void Wander(double delta)
    {
        _wanderTimer -= (float)delta;
        if (_wanderTimer <= 0)
        {
            SetNewWanderTarget();
            _wanderTimer = 3.0f + (float)_rng.NextDouble() * 5.0f;
        }
        MoveTowards(_wanderTarget, delta, BaseSpeed * _speedMultiplier * 0.4f);
    }

    private void MoveTowards(Vector3 target, double delta, float speed)
    {
        Vector3 dir = (target - GlobalPosition).Normalized();
        
        // --- Ghost Braid: Slide and Grip Paradigm (Patch xv) ---
        Vector3 targetHorizontalVel = dir * speed;
        Vector3 currentHorizontalVel = new Vector3(Velocity.X, 0, Velocity.Z);
        
        float traction = 1.0f;
        
        if (currentHorizontalVel.Length() > 0.5f)
        {
            float dot = dir.Normalized().Dot(currentHorizontalVel.Normalized());
            
            // Zombies slide wide on sharp turns (more aggressively than the player)
            if (dot < 0.85f) 
            {
                traction = 0.15f; // Step-change in Mu, triggering a slide
                
                // Visual extrusion
                if (AmbientFXController.Instance != null && _rng.NextDouble() < 0.05)
                {
                    AmbientFXController.Instance.TriggerSlideScrape(GlobalPosition);
                }
            }
        }
        
        Velocity = new Vector3(
            Mathf.Lerp(Velocity.X, targetHorizontalVel.X, 10.0f * traction * (float)delta),
            Velocity.Y,
            Mathf.Lerp(Velocity.Z, targetHorizontalVel.Z, 10.0f * traction * (float)delta)
        );
        
        MoveAndSlide();
        
        if (currentHorizontalVel.LengthSquared() > 0.01f)
            LookAt(GlobalPosition + currentHorizontalVel, Vector3.Up);
    }

    private void SetNewWanderTarget()
    {
        float angle = (float)_rng.NextDouble() * Mathf.Pi * 2.0f;
        float dist = 5.0f + (float)_rng.NextDouble() * 15.0f;
        _wanderTarget = GlobalPosition + new Vector3(Mathf.Cos(angle) * dist, 0, Mathf.Sin(angle) * dist);
    }

    /// <summary>
    /// Called when player attacks this zombie. Returns true if killed.
    /// </summary>
    public bool TakeDamage(float damage, string weaponType = "melee")
    {
        float effectiveHealth = Health * _healthMultiplier;
        float actualDamage = weaponType == "ranged" ? damage * 1.5f : damage;
        effectiveHealth -= actualDamage;

        if (effectiveHealth <= 0)
        {
            QueueFree();
            ConstraintField.EmitNoise(0.5f);
            return true;
        }

        // Hit reaction: temporarily flee
        _stamina = 5.0f;
        return false;
    }

    /// <summary>
    /// Infect the player on bite contact.
    /// </summary>
    public void TryInfectPlayer()
    {
        // Infection chance based on tier
        float infectionChance = Tier switch
        {
            ZombieTier.Brute => 0.8f,
            ZombieTier.Runner => 0.6f,
            ZombieTier.Walker => 0.4f,
            ZombieTier.Crawler => 0.3f,
            ZombieTier.Spitter => 0.7f,
            ZombieTier.Mutant => 1.0f,
            _ => 0.5f
        };

        if (_rng.NextDouble() < infectionChance)
        {
            ConstraintField.Infection = Mathf.Clamp(ConstraintField.Infection + 0.15f, 0.0f, 1.0f);
            ConstraintField.Morale = Mathf.Clamp(ConstraintField.Morale - 0.1f, 0.0f, 1.0f);
            ConstraintField.EmitNoise(0.8f); // Scream attracts more
        }
    }
}
