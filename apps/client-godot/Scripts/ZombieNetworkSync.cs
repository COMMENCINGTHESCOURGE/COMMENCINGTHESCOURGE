using Godot;

/// <summary>
/// Zombie network synchronization.
/// Server-authoritative. Clients receive interpolated state.
/// Horde events and AI decisions originate from server (AuthorityZone.Server).
/// </summary>
public partial class ZombieNetworkSync : Node
{
    [Export] public float SyncRate = 0.1f; // 10 Hz

    private Zombie _zombie;
    private MultiplayerSynchronizer _sync;

    // Replicated properties
    [ExportGroup("Replicated")]
    [Export] private Vector3 _replicatedPosition;
    [Export] private Vector3 _replicatedVelocity;
    [Export] private float _replicatedRotation;
    [Export] private float _replicatedHealth = 100f;
    [Export] private int _replicatedTier = 0;
    [Export] private bool _replicatedIsAlive = true;

    // Interpolation
    private System.Collections.Generic.List<ZombieState> _stateBuffer = new();
    private float _interpolationTimer = 0f;
    private const float InterpolationDelay = 0.05f;

    public struct ZombieState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Rotation;
        public double Timestamp;
    }

    public override void _Ready()
    {
        _zombie = GetParent<Zombie>();
        if (_zombie == null)
        {
            GD.PrintErr("ZombieNetworkSync: Must be child of Zombie");
            return;
        }

        if (Multiplayer.IsServer())
        {
            // Server: run full AI, sync to clients
            _sync = new MultiplayerSynchronizer();
            _sync.ReplicationInterval = SyncRate;

            _sync.ReplicationConfig = new SceneReplicationConfig();
            _sync.ReplicationConfig.AddProperty(".:_replicatedPosition");
            _sync.ReplicationConfig.AddProperty(".:_replicatedVelocity");
            _sync.ReplicationConfig.AddProperty(".:_replicatedRotation");
            _sync.ReplicationConfig.AddProperty(".:_replicatedHealth");
            _sync.ReplicationConfig.AddProperty(".:_replicatedTier");
            _sync.ReplicationConfig.AddProperty(".:_replicatedIsAlive");

            AddChild(_sync);
            SetProcess(false); // Server doesn't need to interpolate
        }
        else
        {
            // Client: interpolate received state
            SetProcess(true);
            _zombie.SetPhysicsProcess(false); // Disable local AI on remote zombies
        }
    }

    public override void _Process(double delta)
    {
        if (Multiplayer.IsServer()) return;

        _interpolationTimer += (float)delta;
        if (_stateBuffer.Count > 1 && _interpolationTimer >= InterpolationDelay)
        {
            // Remove oldest consumed state
            _stateBuffer.RemoveAt(0);
            _interpolationTimer = 0f;
        }

        if (_stateBuffer.Count > 0)
        {
            // Simple linear interpolation to last known state
            if (_stateBuffer.Count == 1)
            {
                var last = _stateBuffer[0];
                _zombie.Position = last.Position;
                _zombie.Rotation = new Vector3(0, last.Rotation, 0);
            }
            else
            {
                float t = Mathf.Clamp(_interpolationTimer / InterpolationDelay, 0f, 1f);
                var prev = _stateBuffer[0];
                var next = _stateBuffer[1];
                _zombie.Position = prev.Position.Lerp(next.Position, t);
                _zombie.Rotation = new Vector3(0, Mathf.LerpAngle(prev.Rotation, next.Rotation, t), 0);
            }
        }
    }

    /// <summary>
    /// Server pushes zombie state to clients via RPC.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void ReceiveZombieState(Vector3 pos, Vector3 vel, float rot, float health, int tier, bool alive)
    {
        if (Multiplayer.IsServer()) return;

        _replicatedPosition = pos;
        _replicatedVelocity = vel;
        _replicatedRotation = rot;
        _replicatedHealth = health;
        _replicatedTier = tier;
        _replicatedIsAlive = alive;

        // Buffer for interpolation
        var state = new ZombieState
        {
            Position = pos,
            Velocity = vel,
            Rotation = rot,
            Timestamp = Time.GetTicksMsec() / 1000.0
        };
        _stateBuffer.Add(state);
        if (_stateBuffer.Count > 10)
            _stateBuffer.RemoveAt(0);
    }

    /// <summary>
    /// Server broadcasts zombie state every tick.
    /// Call from ZombieSpawner or a manager node on the server.
    /// </summary>
    public void BroadcastZombieStates()
    {
        if (!Multiplayer.IsServer()) return;

        var zombies = GetTree().GetNodesInGroup("Zombies");
        foreach (Zombie z in zombies)
        {
            var sync = z.GetNodeOrNull<ZombieNetworkSync>("ZombieNetworkSync");
            if (sync != null)
            {
                Rpc(nameof(ReceiveZombieState),
                    z.Position, z.Velocity, z.Rotation.Y,
                    z.Health, (int)z.Tier, true);
            }
        }
    }
}
