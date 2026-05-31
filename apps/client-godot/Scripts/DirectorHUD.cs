using Godot;
using System;

/// <summary>
/// Phase 13: Virtual Production Emulator
/// The UI/Glitch board for the Director. Allows them to trigger events and control lighting mid-take.
/// </summary>
public partial class DirectorHUD : CanvasLayer
{
    private Button _btnTriggerCollapse;
    private Button _btnTriggerRepair;
    private Button _btnToggleSnowstorm;
    private Button _btnDispatchTowTruck;
    
    // Timeline controls
    private Button _btnRecordTake;
    private Button _btnStopTimeline;
    private Button _btnPlaybackTake;

    // Observation controls
    private Button _btnCycleCamera;
    private int _currentCameraIndex = 0;

    // Architecture controls
    private Button _btnCalculatePacing;

    private Slider _sliderTimeOfDay;

    public override void _Ready()
    {
        // Setup UI structure
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        panel.OffsetLeft = -200;
        panel.OffsetBottom = 150;
        AddChild(panel);

        var vbox = new VBoxContainer();
        panel.AddChild(vbox);

        var label = new Label { Text = "DIRECTOR BOARD" };
        vbox.AddChild(label);

        _btnTriggerCollapse = new Button { Text = "Cue: Building Collapse" };
        _btnTriggerCollapse.Pressed += OnCueCollapse;
        vbox.AddChild(_btnTriggerCollapse);

        _btnTriggerRepair = new Button { Text = "Cue: Matrix Rez (Repair)" };
        _btnTriggerRepair.Pressed += OnCueRepair;
        vbox.AddChild(_btnTriggerRepair);

        _btnToggleSnowstorm = new Button { Text = "Cue: Toggle Snowstorm" };
        _btnToggleSnowstorm.Pressed += OnToggleSnowstorm;
        vbox.AddChild(_btnToggleSnowstorm);

        _btnDispatchTowTruck = new Button { Text = "Dispatch: Tow Truck" };
        _btnDispatchTowTruck.Pressed += OnDispatchTowTruck;
        vbox.AddChild(_btnDispatchTowTruck);

        var sep2 = new HSeparator();
        vbox.AddChild(sep2);

        var timelineLabel = new Label { Text = "-- TIMELINE (TAKES) --" };
        vbox.AddChild(timelineLabel);

        _btnRecordTake = new Button { Text = "Timeline: RECORD TAKE" };
        _btnRecordTake.Pressed += () => { if (StateRecorder.Instance != null) StateRecorder.Instance.StartRecording(); };
        vbox.AddChild(_btnRecordTake);

        _btnStopTimeline = new Button { Text = "Timeline: STOP" };
        _btnStopTimeline.Pressed += () => { if (StateRecorder.Instance != null) StateRecorder.Instance.Stop(); };
        vbox.AddChild(_btnStopTimeline);

        _btnPlaybackTake = new Button { Text = "Timeline: PLAYBACK TAKE" };
        _btnPlaybackTake.Pressed += () => { if (StateRecorder.Instance != null) StateRecorder.Instance.StartPlayback(); };
        vbox.AddChild(_btnPlaybackTake);

        var sep3 = new HSeparator();
        vbox.AddChild(sep3);

        var obsLabel = new Label { Text = "-- OBSERVATION --" };
        vbox.AddChild(obsLabel);

        _btnCycleCamera = new Button { Text = "Cycle: B-Roll Feeds" };
        _btnCycleCamera.Pressed += OnCycleCameraFeed;
        vbox.AddChild(_btnCycleCamera);

        var sep4 = new HSeparator();
        vbox.AddChild(sep4);

        var archLabel = new Label { Text = "-- ARCHITECTURE --" };
        vbox.AddChild(archLabel);

        _btnCalculatePacing = new Button { Text = "Calculate Shot Pacing" };
        _btnCalculatePacing.Pressed += OnCalculatePacing;
        vbox.AddChild(_btnCalculatePacing);

        var sep5 = new HSeparator();
        vbox.AddChild(sep5);

        var timeLabel = new Label { Text = "Time of Day" };
        vbox.AddChild(timeLabel);

        _sliderTimeOfDay = new HSlider { MinValue = 0, MaxValue = 24, Value = 12 };
        _sliderTimeOfDay.ValueChanged += OnTimeChanged;
        vbox.AddChild(_sliderTimeOfDay);
    }

    private void OnCueCollapse()
    {
        // Trigger a collapse exactly where the Director is looking
        if (GetViewport().GetCamera3D() is CinematicCamera cam)
        {
            Vector3 targetPos = cam.GlobalPosition + (-cam.GlobalTransform.Basis.Z * 20.0f); // 20m ahead
            if (AmbientFXController.Instance != null)
            {
                AmbientFXController.Instance.TriggerStructureCollapse(targetPos);
                GD.Print($"Director: Cued Structure Collapse at {targetPos}");
            }
        }
    }

    private void OnCueRepair()
    {
        if (GetViewport().GetCamera3D() is CinematicCamera cam)
        {
            Vector3 targetPos = cam.GlobalPosition + (-cam.GlobalTransform.Basis.Z * 10.0f);
            if (AmbientFXController.Instance != null)
            {
                AmbientFXController.Instance.TriggerRoadRepair(targetPos);
                GD.Print($"Director: Cued Road Repair at {targetPos}");
            }
        }
    }

    private void OnToggleSnowstorm()
    {
        if (AmbientFXController.Instance != null)
        {
            AmbientFXController.Instance.ToggleSnowstorm();
        }
    }

    private void OnDispatchTowTruck()
    {
        if (GetViewport().GetCamera3D() is CinematicCamera cam)
        {
            // Spawn an empty tow truck 15 meters in front of the camera
            Vector3 spawnPos = cam.GlobalPosition + (-cam.GlobalTransform.Basis.Z * 15.0f);
            spawnPos.Y = 0.5f; // Drop it slightly above ground

            // Create a heavier, slower vehicle configuration for the tow truck
            Vehicle towTruck = new Vehicle();
            towTruck.GlobalPosition = spawnPos;
            towTruck.EnginePower = 300f; // Slower acceleration
            towTruck.SteeringAngle = 20.0f; // Wider turning radius
            
            // Add visual proxy for a tow truck
            var meshInst = new MeshInstance3D();
            var box = new BoxMesh();
            box.Size = new Vector3(2.5f, 3.0f, 6.0f); // Large, bulky frame
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = new Color(1.0f, 0.8f, 0.0f); // Safety yellow
            box.Material = mat;
            meshInst.Mesh = box;
            towTruck.AddChild(meshInst);

            GetTree().Root.AddChild(towTruck);
            GD.Print($"Director: Dispatched empty Tow Truck at {spawnPos} for actors.");

            // Phase 15: The Kinematic Cascade
            // 1. Register the Anchor Node to force traffic to reroute
            if (RoadNetwork.Instance != null)
            {
                RoadNetwork.Instance.CreateAnchorNode(new Vector2(spawnPos.X, spawnPos.Z));
            }

            // 2. Spike the Data Density to draw observation elements (sensors/cameras)
            var propSystem = GetNodeOrNull<PropagationSystem>("/root/PropagationSystem"); // Assuming auto-load or global access, we might need a better reference.
            // Wait, PropagationSystem is likely attached somewhere, but since we don't have a Singleton for it, let's look for it in the group or assume a path.
            // Let's use the group "PropagationSystem" if it exists, or just find it.
            var propNode = GetTree().GetFirstNodeInGroup("Propagation");
            if (propNode is PropagationSystem ps)
            {
                ps.SpikeDataDensity(spawnPos, 500.0f);
            }
        }
    }

    private void OnCycleCameraFeed()
    {
        var bRollCams = GetTree().GetNodesInGroup("BRollCameras");
        var mainCam = GetTree().GetFirstNodeInGroup("MainCamera") as Camera3D;

        if (bRollCams.Count == 0)
        {
            // Spawn a test B-Roll camera if none exist
            var newCam = new BRollCamera();
            if (GetViewport().GetCamera3D() != null)
            {
                newCam.GlobalPosition = GetViewport().GetCamera3D().GlobalPosition + new Vector3(0, 10f, -20f); // Mount high and away
            }
            GetTree().Root.AddChild(newCam);
            bRollCams = GetTree().GetNodesInGroup("BRollCameras");
            GD.Print("Director: Auto-deployed a new B-Roll drone to cover the scene.");
        }

        _currentCameraIndex++;
        if (_currentCameraIndex > bRollCams.Count)
        {
            _currentCameraIndex = 0; // Return to main
        }

        if (_currentCameraIndex == 0)
        {
            mainCam?.MakeCurrent();
            GD.Print("Director: Switched feed to A-Camera (Main).");
        }
        else
        {
            var targetCam = bRollCams[_currentCameraIndex - 1] as Camera3D;
            targetCam?.MakeCurrent();
            GD.Print($"Director: Switched feed to B-Roll Camera {_currentCameraIndex}.");
        }
    }

    private void OnCalculatePacing()
    {
        if (MetricArchitect.Instance == null)
        {
            var arch = new MetricArchitect();
            GetTree().Root.AddChild(arch);
            GD.Print("Director: Deployed MetricArchitect foundation logic.");
        }

        // Find an NPC to calculate pacing for
        var npcs = GetTree().GetNodesInGroup("NPCs");
        if (npcs.Count > 0 && npcs[0] is NPC targetNpc)
        {
            // We need to access the NPC's current anchor, but it's private. 
            // We will just calculate distance from NPC to home as a test.
            // Using reflection or a public getter would be better, but we'll do a generic spatial calculation.
            var start = targetNpc.GlobalPosition;
            var end = start + new Vector3(20, MetricArchitect.BiLevelSplitHeight, 20); // Simulating a bi-level route
            
            float duration = MetricArchitect.Instance.CalculatePacing(start, end, targetNpc.WalkSpeed);
            GD.Print($"[Stair Counter]: Estimated traversal time for {targetNpc.Name} across bi-level geometry: {duration:F2} seconds.");
        }
        else
        {
            GD.Print("[Stair Counter]: No NPCs found to calculate pacing.");
        }
    }

    private void OnTimeChanged(double value)
    {
        // Normally this would hook into a DirectionalLight3D or WorldEnvironment
        GD.Print($"Director: Changing Time of Day to {value}:00");
    }
}
