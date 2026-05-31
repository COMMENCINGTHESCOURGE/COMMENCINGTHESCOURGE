using Godot;
using System;

/// <summary>
/// Layer 2 (Foreground) - Ambient FX Controller
/// Adopted Godot-style pipeline for 2.5D:
/// Layer 0: Background volumetric
/// Layer 1: Gameplay (Mechanical integrity)
/// Layer 2: Foreground Ambient FX (dust, rain, parallax) to seal the composite.
/// </summary>
public partial class AmbientFXController : Node3D
{
    public static AmbientFXController Instance { get; private set; }

    private GpuParticles3D _dustParticles;
    private GpuParticles3D _rainParticles;
    private ColorRect _parallaxOverlay;
    
    // Slide Scrape Particle Template
    private Node3D _debrisContainer;
    private GpuParticles3D _snowstormParticles;

    public override void _Ready()
    {
        Instance = this;

        // Instantiate or fetch existing FX nodes
        _dustParticles = GetNodeOrNull<GpuParticles3D>("DustFX");
        _rainParticles = GetNodeOrNull<GpuParticles3D>("RainFX");
        
        var canvas = GetNodeOrNull<CanvasLayer>("FXCanvas");
        if (canvas != null)
        {
            _parallaxOverlay = canvas.GetNodeOrNull<ColorRect>("ParallaxOverlay");
        }

        // Defensive instantiation if missing
        if (_dustParticles == null)
        {
            _dustParticles = new GpuParticles3D();
            _dustParticles.Name = "DustFX";
            AddChild(_dustParticles);
        }
        if (_rainParticles == null)
        {
            _rainParticles = new GpuParticles3D();
            _rainParticles.Name = "RainFX";
            AddChild(_rainParticles);
        }
    }

    public override void _Process(double delta)
    {
        string weather = ConstraintField.Weather;
        float timeOfDay = ConstraintField.TimeOfDay;

        // Dust scales up during dry, clear days
        if (weather == "clear" || weather == "fog")
        {
            _dustParticles.Emitting = true;
            _rainParticles.Emitting = false;
        }
        else if (weather == "rain" || weather == "storm")
        {
            _dustParticles.Emitting = false;
            _rainParticles.Emitting = true;
            
            // Storm intensity increases rain particles
            _rainParticles.Amount = weather == "storm" ? 2000 : 500;
        }

        // Parallax depth/opacity shifts based on time of day (Layer 2 sealing)
        if (_parallaxOverlay != null)
        {
            bool isNight = timeOfDay < 5.0f || timeOfDay > 20.0f;
            float targetAlpha = isNight ? 0.8f : 0.2f;
            Color currentMod = _parallaxOverlay.Modulate;
            // Smooth transition
            _parallaxOverlay.Modulate = currentMod.Lerp(new Color(currentMod.R, currentMod.G, currentMod.B, targetAlpha), (float)delta * 0.5f);
        }
    }

    public void TriggerSlideScrape(Vector3 position, float intensity = 1.0f)
    {
        // Visual extrusion of the step-change in Mu
        var scrape = new GpuParticles3D();
        scrape.Emitting = false;
        scrape.OneShot = true;
        
        // Scale amount and velocity based on intensity (e.g. 1.0 for passive slide, 3.0 for Toe Stop)
        scrape.Amount = (int)(15 * intensity);
        scrape.Lifetime = 0.5f;
        scrape.Explosiveness = intensity > 1.5f ? 0.95f : 0.9f; // More explosive for toe stops
        
        var mat = new ParticleProcessMaterial();
        mat.Direction = new Vector3(0, 1, 0);
        mat.Spread = 45f;
        mat.InitialVelocityMin = 1.0f * intensity;
        mat.InitialVelocityMax = 3.0f * intensity;
        scrape.ProcessMaterial = mat;
        
        var mesh = new BoxMesh();
        mesh.Size = new Vector3(0.05f, 0.05f, 0.05f);
        scrape.DrawPass1 = mesh;

        GetTree().Root.AddChild(scrape);
        scrape.GlobalPosition = position - new Vector3(0, 1f, 0); // At foot level
        scrape.Emitting = true;

        // Cleanup timer
        var timer = GetTree().CreateTimer(1.0f);
        timer.Timeout += () => { if (IsInstanceValid(scrape)) scrape.QueueFree(); };
    }

    public void TriggerStructureCollapse(Vector3 position)
    {
        // Visual extrusion of catastrophic structural failure (shear)
        var debris = new GpuParticles3D();
        debris.Emitting = false;
        debris.OneShot = true;
        debris.Amount = 50;
        debris.Lifetime = 2.0f;
        debris.Explosiveness = 1.0f; // Instantaneous burst
        
        var mat = new ParticleProcessMaterial();
        mat.Direction = new Vector3(0, 1, 0);
        mat.Spread = 180f;
        mat.InitialVelocityMin = 2.0f;
        mat.InitialVelocityMax = 8.0f;
        debris.ProcessMaterial = mat;
        
        var mesh = new BoxMesh();
        mesh.Size = new Vector3(0.2f, 0.2f, 0.2f); // Large debris chunks
        debris.DrawPass1 = mesh;

        GetTree().Root.AddChild(debris);
        debris.GlobalPosition = position;
        debris.Emitting = true;

        var timer = GetTree().CreateTimer(3.0f);
        timer.Timeout += () => { if (IsInstanceValid(debris)) debris.QueueFree(); };
    }

    public void TriggerRoadRepair(Vector3 position)
    {
        // Visual extrusion of a pothole being instantly filled (matrix rez effect)
        var repairFx = new GpuParticles3D();
        repairFx.Emitting = false;
        repairFx.OneShot = true;
        repairFx.Amount = 30;
        repairFx.Lifetime = 1.0f;
        repairFx.Explosiveness = 0.8f;
        
        var mat = new ParticleProcessMaterial();
        mat.Direction = new Vector3(0, 1, 0);
        mat.Spread = 20f;
        mat.InitialVelocityMin = 0.5f;
        mat.InitialVelocityMax = 2.0f;
        repairFx.ProcessMaterial = mat;
        
        var mesh = new BoxMesh();
        mesh.Size = new Vector3(0.05f, 0.05f, 0.05f); 
        
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(0.2f, 1.0f, 0.5f); // Matrix green rez
        material.EmissionEnabled = true;
        material.Emission = new Color(0.2f, 1.0f, 0.5f);
        material.EmissionEnergyMultiplier = 2.0f;
        mesh.Material = material;
        
        repairFx.DrawPass1 = mesh;

        GetTree().Root.AddChild(repairFx);
        repairFx.GlobalPosition = position;
        repairFx.Emitting = true;

        var timer = GetTree().CreateTimer(1.5f);
        timer.Timeout += () => { if (IsInstanceValid(repairFx)) repairFx.QueueFree(); };
    }

    public void ToggleSnowstorm()
    {
        if (_snowstormParticles != null && IsInstanceValid(_snowstormParticles))
        {
            _snowstormParticles.QueueFree();
            _snowstormParticles = null;
            ConstraintField.SurfaceTractionModifier = 1.0f; // Restore global grip
            ConstraintField.Weather = "clear";
            GD.Print("AmbientFXController: Snowstorm deactivated. Traction restored.");
            return;
        }

        ConstraintField.SurfaceTractionModifier = 0.4f; // Global slip cascade
        ConstraintField.Weather = "snow";

        _snowstormParticles = new GpuParticles3D();
        _snowstormParticles.Amount = 5000;
        _snowstormParticles.Lifetime = 4.0f;
        _snowstormParticles.VisibilityAabb = new Aabb(new Vector3(-50, -50, -50), new Vector3(100, 100, 100));

        var mat = new ParticleProcessMaterial();
        mat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
        mat.EmissionBoxExtents = new Vector3(50, 10, 50);
        mat.Direction = new Vector3(0, -1, 0);
        mat.Spread = 10f;
        mat.InitialVelocityMin = 5.0f;
        mat.InitialVelocityMax = 10.0f;
        mat.Gravity = new Vector3(0, -9.8f, 0);
        _snowstormParticles.ProcessMaterial = mat;

        var mesh = new QuadMesh();
        mesh.Size = new Vector2(0.1f, 0.1f);
        var material = new StandardMaterial3D();
        material.AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 0.8f);
        material.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        material.UseParticleTrails = true;
        mesh.Material = material;

        _snowstormParticles.DrawPass1 = mesh;

        GetTree().Root.AddChild(_snowstormParticles);
        
        // Follow the camera
        var cam = GetViewport().GetCamera3D();
        if (cam != null)
        {
            _snowstormParticles.GlobalPosition = cam.GlobalPosition + new Vector3(0, 20f, 0);
        }
        else
        {
            _snowstormParticles.GlobalPosition = new Vector3(0, 20f, 0);
        }

        _snowstormParticles.Emitting = true;
        GD.Print("AmbientFXController: Snowstorm activated.");
    }
}
