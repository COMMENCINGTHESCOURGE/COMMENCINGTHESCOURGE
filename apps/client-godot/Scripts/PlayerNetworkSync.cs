using Godot;

/// <summary>
/// Player network synchronization.
/// Handles position replication, state interpolation,
/// client-side prediction (input-based), and authority checks.
/// 
/// Authority model:
///   - Owning peer: sends input/state to server
///   - Server: validates and broadcasts authoritative position
///   - Non-owning peers: interpolate received state
/// </summary>
public partial class PlayerNetworkSync : Node
{
    [Export] public float StateSendRate = 0.05f;  // 20 Hz state update
    [Export] public float InterpolationDelay = 0.1f; // 100ms interpolation buffer

    private Player _player;
    private MultiplayerSynchronizer _sync;
    private int _ownerId;

    // Network state
    private Vector3 _lastSentPos;
    private Vector3 _lastSentVel;
    private float _lastSentRotation;

    // Interpolation state (for remote players)
    private struct StateSnapshot
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Rotation;
        public double Timestamp;
    }
    private System.Collections.Generic.List<StateSnapshot> _stateBuffer = new();
    private StateSnapshot _previousState;
    private StateSnapshot _nextState;
    private double _interpolationTimer = 0.0;

    // Client-side prediction
    private System.Collections.Generic.List<InputSnapshot> _pendingInputs = new();

    public struct InputSnapshot
    {
        public Vector2 InputDir;
        public bool JumpPressed;
        public bool SprintHeld;
        public double Timestamp;
    }

    // ─── Replicated Properties ───
    [ExportGroup("Replicated")]
    [Export] private Vector3 _replicatedPosition;
    [Export] private Vector3 _replicatedVelocity;
    [Export] private float _replicatedRotation;
    [Export] private float _replicatedHealth = 100f;
    [Export] private float _replicatedHunger = 1f;
    [Export] private float _replicatedThirst = 1f;

    public override void _Ready()
    {
        _player = GetParent<Player>();
        if (_player == null)
        {
            GD.PrintErr("PlayerNetworkSync: Must be child of Player");
            return;
        }

        _ownerId = Multiplayer.GetRemoteSenderId();
        if (_ownerId == 0) _ownerId = 1; // Local or host

        // Add MultiplayerSynchronizer
        _sync = new MultiplayerSynchronizer();
        _sync.ReplicationInterval = StateSendRate;

        // Register replicated properties in Godot 4.3 using SceneReplicationConfig
        _sync.ReplicationConfig = new SceneReplicationConfig();
        _sync.ReplicationConfig.AddProperty(".:_replicatedPosition");
        _sync.ReplicationConfig.AddProperty(".:_replicatedVelocity");
        _sync.ReplicationConfig.AddProperty(".:_replicatedRotation");
        _sync.ReplicationConfig.AddProperty(".:_replicatedHealth");
        _sync.ReplicationConfig.AddProperty(".:_replicatedHunger");
        _sync.ReplicationConfig.AddProperty(".:_replicatedThirst");

        AddChild(_sync);

        // Set visibility based on ownership
        if (_ownerId == Multiplayer.GetUniqueId())
        {
            // Local player: full control
            SetProcess(false); // Don't process interpolation for self
        }
        else
        {
            // Remote player: interpolate
            SetProcess(true);
            _player.ProcessMode = ProcessModeEnum.Disabled; // Disable local input processing
            _player.SetPhysicsProcess(false);
        }
    }

    public override void _Process(double delta)
    {
        // Only run for remote players (interpolation)
        if (_ownerId == Multiplayer.GetUniqueId()) return;

        _interpolationTimer += delta;
        if (_interpolationTimer >= InterpolationDelay && _stateBuffer.Count > 1)
        {
            // Pop oldest state
            _previousState = _stateBuffer[0];
            _stateBuffer.RemoveAt(0);
            _nextState = _stateBuffer[0];
            _interpolationTimer = 0.0;
        }

        // Interpolate between previous and next state
        if (_stateBuffer.Count > 0)
        {
            double t = Mathf.Clamp(_interpolationTimer / InterpolationDelay, 0.0, 1.0);
            _player.Position = _previousState.Position.Lerp(_nextState.Position, (float)t);
            _player.Velocity = _previousState.Velocity.Lerp(_nextState.Velocity, (float)t);
            _player.Rotation = new Vector3(0, Mathf.LerpAngle(_previousState.Rotation, _nextState.Rotation, (float)t), 0);
        }
    }

    /// <summary>
    /// Server receives remote input and simulates authoritative state.
    /// Called via RPC from owning client.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ReceiveInput(Vector2 inputDir, bool jumpPressed, bool sprintHeld, double clientTimestamp)
    {
        int sender = Multiplayer.GetRemoteSenderId();
        if (sender != _ownerId) return; // Only accept from owner

        // TODO: Apply input to server-side player simulation
        // For now, just acknowledge and update replicated state
        _replicatedPosition = _player.Position;
        _replicatedVelocity = _player.Velocity;
        _replicatedRotation = _player.Rotation.Y;
    }

    /// <summary>
    /// Send local input to server (for client-side prediction reconciliation).
    /// </summary>
    public void SendInputToServer(Vector2 inputDir, bool jumpPressed, bool sprintHeld)
    {
        if (!Multiplayer.IsServer())
        {
            // Send input to server
            RpcId(1, nameof(ReceiveInput), inputDir, jumpPressed, sprintHeld, Time.GetTicksMsec() / 1000.0);
        }
    }

    /// <summary>
    /// Called by server to reconcile client state.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ServerReconcile(Vector3 serverPos, Vector3 serverVel, float serverRotation)
    {
        int sender = Multiplayer.GetRemoteSenderId();
        if (sender != 1) return; // Only accept from server

        _replicatedPosition = serverPos;
        _replicatedVelocity = serverVel;
        _replicatedRotation = serverRotation;

        // Apply reconciliation
        _player.Position = serverPos;
        _player.Velocity = serverVel;
    }

    /// <summary>
    /// Push state to remote players (called by owning peer or server).
    /// </summary>
    public void PushState(Vector3 position, Vector3 velocity, float rotation)
    {
        if (Multiplayer.IsServer() || _ownerId == Multiplayer.GetUniqueId())
        {
            _replicatedPosition = position;
            _replicatedVelocity = velocity;
            _replicatedRotation = rotation;
        }
    }

    /// <summary>
    /// Receive state from server for interpolation.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ReceiveState(Vector3 pos, Vector3 vel, float rot, float health, float hunger, float thirst)
    {
        int sender = Multiplayer.GetRemoteSenderId();
        if (sender != 1) return; // Only from server

        // Buffer for interpolation
        var snapshot = new StateSnapshot
        {
            Position = pos,
            Velocity = vel,
            Rotation = rot,
            Timestamp = Time.GetTicksMsec() / 1000.0
        };

        _stateBuffer.Add(snapshot);
        if (_stateBuffer.Count > 10)
            _stateBuffer.RemoveAt(0); // Cap buffer

        // Update replicated values
        _replicatedHealth = health;
        _replicatedHunger = hunger;
        _replicatedThirst = thirst;
    }

    /// <summary>
    /// Update remote state from owning peer's current values.
    /// Call this from Player._PhysicsProcess on the authoritative peer.
    /// </summary>
    public void UpdateReplicatedState(Vector3 position, Vector3 velocity, float rotation, 
        float health, float hunger, float thirst)
    {
        if (_sync != null && (_ownerId == Multiplayer.GetUniqueId() || Multiplayer.IsServer()))
        {
            _replicatedPosition = position;
            _replicatedVelocity = velocity;
            _replicatedRotation = rotation;
            _replicatedHealth = health;
            _replicatedHunger = hunger;
            _replicatedThirst = thirst;
        }
    }
}
