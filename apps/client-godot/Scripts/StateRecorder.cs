using Godot;
using System.Collections.Generic;

/// <summary>
/// Phase 16: The Deterministic Timeline
/// Records and replays Node3D transforms for Virtual Production.
/// </summary>
public partial class StateRecorder : Node
{
    public static StateRecorder Instance { get; private set; }

    public enum RecorderState
    {
        Idle,
        Recording,
        Playback
    }

    public RecorderState CurrentState { get; private set; } = RecorderState.Idle;
    
    // Store timeline ticks. Dict mapping Node Path or ID to a list of Transforms
    private Dictionary<string, List<Transform3D>> _timelineData = new Dictionary<string, List<Transform3D>>();
    private int _currentPlaybackTick = 0;
    
    public override void _Ready()
    {
        Instance = this;
    }

    public void StartRecording()
    {
        _timelineData.Clear();
        CurrentState = RecorderState.Recording;
        GD.Print("StateRecorder: STARTED RECORDING");
    }

    public void Stop()
    {
        CurrentState = RecorderState.Idle;
        _currentPlaybackTick = 0;
        GD.Print("StateRecorder: STOPPED");
    }

    public void StartPlayback()
    {
        if (_timelineData.Count > 0)
        {
            CurrentState = RecorderState.Playback;
            _currentPlaybackTick = 0;
            GD.Print("StateRecorder: STARTED PLAYBACK");
        }
        else
        {
            GD.Print("StateRecorder: No data to play back.");
        }
    }

    public void RecordTransform(string id, Transform3D transform)
    {
        if (CurrentState != RecorderState.Recording) return;

        if (!_timelineData.ContainsKey(id))
        {
            _timelineData[id] = new List<Transform3D>();
        }
        _timelineData[id].Add(transform);
    }

    public bool TryGetPlaybackTransform(string id, out Transform3D transform)
    {
        transform = Transform3D.Identity;
        if (CurrentState != RecorderState.Playback) return false;

        if (_timelineData.ContainsKey(id))
        {
            var timeline = _timelineData[id];
            if (_currentPlaybackTick < timeline.Count)
            {
                transform = timeline[_currentPlaybackTick];
                return true;
            }
            else if (timeline.Count > 0)
            {
                // Hold the last frame if we overshoot
                transform = timeline[timeline.Count - 1];
                return true;
            }
        }
        return false;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Advance the playback head
        if (CurrentState == RecorderState.Playback)
        {
            _currentPlaybackTick++;
            
            // Check if all timelines have finished
            bool isFinished = true;
            foreach (var timeline in _timelineData.Values)
            {
                if (_currentPlaybackTick < timeline.Count)
                {
                    isFinished = false;
                    break;
                }
            }

            if (isFinished)
            {
                Stop(); // Auto-stop when take finishes
            }
        }
    }
}
