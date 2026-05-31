using Godot;
using System.Collections.Generic;

/// <summary>
/// Multiplayer session manager.
/// Handles lobby creation, player joining, connection management,
/// and authority zoning for the vinc-engine.
/// </summary>
public partial class MultiplayerManager : Node
{
    // ─── Signals ───
    [Signal]
    public delegate void PlayerJoinedEventHandler(int peerId, string playerName);
    [Signal]
    public delegate void PlayerLeftEventHandler(int peerId);
    [Signal]
    public delegate void SessionStartedEventHandler();
    [Signal]
    public delegate void SessionEndedEventHandler();

    // ─── Configuration ───
    [Export] public int MaxPlayers = 4;
    [Export] public int DefaultPort = 34197;  // Standard game port
    [Export] public string ServerAddress = "127.0.0.1";

    // ─── State ───
    public bool IsServer { get; private set; } = false;
    public bool IsClient { get; private set; } = false;
    public bool IsHost { get; private set; } = false;
    public int LocalPeerId => Multiplayer.GetUniqueId();
    public int PlayerCount => _players.Count;

    private Dictionary<int, PlayerInfo> _players = new();
    private ENetMultiplayerPeer _peer;

    // ─── Authority Zones ───
    // Each zone has one authoritative peer. Non-author peers send RPCs.
    public enum AuthorityZone
    {
        Server,         // World state, game rules, NPC AI, zombie director
        PlayerLocal,    // Player movement, inventory, crafting, building placement
        ClientSim,      // Client-side prediction zone (zombie interpolation)
        Spectator       // Read-only
    }

    public enum ClientRole
    {
        Actor,          // Plays the game, spawns as Player or Vehicle
        Director        // Uses CinematicCamera to film, can trigger Matrix events
    }

    // Player info struct
    public struct PlayerInfo
    {
        public int PeerId;
        public string Name;
        public bool Ready;
        public AuthorityZone Zone;
        public ClientRole Role;
    }

    public override void _Ready()
    {
        // Auto-register RPCs
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
    }

    // ─── Host Game ───
    public void HostGame()
    {
        _peer = new ENetMultiplayerPeer();
        Error err = _peer.CreateServer(DefaultPort, MaxPlayers);
        if (err != Error.Ok)
        {
            GD.PrintErr($"Multiplayer: Failed to create server: {err}");
            return;
        }

        Multiplayer.MultiplayerPeer = _peer;
        IsServer = true;
        IsHost = true;
        IsClient = true;

        // Register host as player 1
        RegisterPlayer(Multiplayer.GetUniqueId(), "Host");

        GD.Print($"Multiplayer: Server started on port {DefaultPort}");
    }

    // ─── Join Game ───
    public void JoinGame(string address = null)
    {
        if (address != null) ServerAddress = address;

        _peer = new ENetMultiplayerPeer();
        Error err = _peer.CreateClient(ServerAddress, DefaultPort);
        if (err != Error.Ok)
        {
            GD.PrintErr($"Multiplayer: Failed to connect to {ServerAddress}:{DefaultPort}: {err}");
            return;
        }

        Multiplayer.MultiplayerPeer = _peer;
        IsClient = true;
        IsServer = false;

        GD.Print($"Multiplayer: Connecting to {ServerAddress}:{DefaultPort}...");
    }

    // ─── Disconnect ───
    public void DisconnectFromServer()
    {
        if (_peer != null)
        {
            _peer.Close();
            _peer = null;
        }

        IsServer = false;
        IsClient = false;
        IsHost = false;
        _players.Clear();

        EmitSignal(SignalName.SessionEnded);
        GD.Print("Multiplayer: Disconnected");
    }

    // ─── Connection Callbacks ───
    private void OnPeerConnected(long id)
    {
        int peerId = (int)id;
        GD.Print($"Multiplayer: Peer {peerId} connected");

        if (IsServer)
        {
            // Send current world state to new player
            RpcId(peerId, nameof(ReceiveWorldState), SerializeWorldState());
            
            // Request player name
            RpcId(peerId, nameof(RequestPlayerInfo));
        }
    }

    private void OnPeerDisconnected(long id)
    {
        int peerId = (int)id;
        GD.Print($"Multiplayer: Peer {peerId} disconnected");

        if (_players.Remove(peerId))
        {
            EmitSignal(SignalName.PlayerLeft, peerId);
            
            // Transfer authority if this peer had zone authority
            ReassignAuthority(peerId);
        }
    }

    private void OnConnectedToServer()
    {
        GD.Print("Multiplayer: Connected to server");
        EmitSignal(SignalName.SessionStarted);
        
        // Send our info to server
        RpcId(1, nameof(RegisterPlayer), Multiplayer.GetUniqueId(), GetPlayerName());
    }

    private void OnConnectionFailed()
    {
        GD.PrintErr("Multiplayer: Connection failed");
    }

    private void OnServerDisconnected()
    {
        GD.Print("Multiplayer: Server disconnected");
        DisconnectFromServer();
    }

    // ─── RPCs ───
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RegisterPlayer(int peerId, string playerName)
    {
        if (!IsServer) return;

        if (_players.Count >= MaxPlayers)
        {
            RpcId(peerId, nameof(RejectJoin), "Server is full");
            return;
        }

        var info = new PlayerInfo
        {
            PeerId = peerId,
            Name = playerName,
            Ready = false,
            Zone = AuthorityZone.PlayerLocal,
            Role = playerName.Contains("Director") ? ClientRole.Director : ClientRole.Actor
        };
        _players[peerId] = info;

        GD.Print($"Multiplayer: {playerName} (peer {peerId}) registered");

        // Broadcast to all clients
        Rpc(nameof(OnPlayerRegistered), peerId, playerName);
        EmitSignal(SignalName.PlayerJoined, peerId, playerName);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void OnPlayerRegistered(int peerId, string playerName)
    {
        if (!_players.ContainsKey(peerId))
        {
            _players[peerId] = new PlayerInfo
            {
                PeerId = peerId,
                Name = playerName,
                Ready = false,
                Zone = AuthorityZone.PlayerLocal,
                Role = playerName.Contains("Director") ? ClientRole.Director : ClientRole.Actor
            };
        }
        EmitSignal(SignalName.PlayerJoined, peerId, playerName);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void RequestPlayerInfo()
    {
        int sender = Multiplayer.GetRemoteSenderId();
        RpcId(sender, nameof(ReceivePlayerInfo), Multiplayer.GetUniqueId(), GetPlayerName());
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void ReceivePlayerInfo(int peerId, string playerName)
    {
        if (IsServer)
        {
            RegisterPlayer(peerId, playerName);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void RejectJoin(string reason)
    {
        GD.PrintErr($"Multiplayer: Join rejected: {reason}");
        DisconnectFromServer();
    }

    // ─── Authority Zone Management ───
    /// <summary>
    /// Get the authoritative peer for a given zone.
    /// Server always owns Server zone.
    /// Each client owns their PlayerLocal zone.
    /// </summary>
    public int GetZoneAuthority(AuthorityZone zone)
    {
        if (zone == AuthorityZone.Server)
            return 1; // Server is always peer 1

        if (zone == AuthorityZone.PlayerLocal)
            return LocalPeerId;

        // ClientSim and Spectator are server-authoritative by default
        return 1;
    }

    /// <summary>
    /// Transfer authority when a player disconnects.
    /// PlayerLocal zone transfers to server.
    /// </summary>
    private void ReassignAuthority(int disconnectedPeer)
    {
        // Find what the disconnected peer owned
        foreach (var kvp in _players)
        {
            if (kvp.Key == disconnectedPeer)
            {
                // Their PlayerLocal zone reverts to server
                var info = kvp.Value;
                info.Zone = AuthorityZone.Server;
                _players[kvp.Key] = info;
                break;
            }
        }
    }

    /// <summary>
    /// Check if the local peer has authority over a given zone.
    /// </summary>
    public bool HasAuthority(AuthorityZone zone)
    {
        return GetZoneAuthority(zone) == LocalPeerId;
    }

    /// <summary>
    /// Check if local peer is allowed to modify an entity owned by a specific peer.
    /// </summary>
    public bool CanModify(int ownerPeerId)
    {
        return ownerPeerId == LocalPeerId || IsServer;
    }

    // ─── World State Sync (Delta Compression) ───
    private string SerializeWorldState()
    {
        // Build a snapshot of authoritative game state
        var state = new Dictionary<string, object>
        {
            ["day"] = ConstraintField.Day,
            ["time"] = ConstraintField.TimeOfDay,
            ["weather"] = ConstraintField.Weather,
            ["season"] = ConstraintField.Season,
            ["zombie_count"] = ConstraintField.ZombieCount,
            ["horde_threat"] = ConstraintField.HordeThreat,
            ["population"] = ConstraintField.Population,
            ["currency_trust"] = ConstraintField.CurrencyTrust,
            ["players"] = SerializePlayerList()
        };

        return System.Text.Json.JsonSerializer.Serialize(state);
    }

    private string SerializePlayerList()
    {
        var list = new List<Dictionary<string, object>>();
        foreach (var kvp in _players)
        {
            list.Add(new Dictionary<string, object>
            {
                ["peer_id"] = kvp.Key,
                ["name"] = kvp.Value.Name,
                ["ready"] = kvp.Value.Ready
            });
        }
        return System.Text.Json.JsonSerializer.Serialize(list);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void ReceiveWorldState(string jsonState)
    {
        // Deserialize and apply world state on client
        try
        {
            var state = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonState);
            if (state == null) return;

            if (state.TryGetValue("day", out var day))
                ConstraintField.Day = System.Convert.ToInt32(day);
            if (state.TryGetValue("time", out var time))
                ConstraintField.TimeOfDay = System.Convert.ToSingle(time);
            if (state.TryGetValue("weather", out var weather))
                ConstraintField.Weather = weather?.ToString() ?? "clear";
            if (state.TryGetValue("zombie_count", out var zc))
                ConstraintField.ZombieCount = System.Convert.ToInt32(zc);
            if (state.TryGetValue("horde_threat", out var ht))
                ConstraintField.HordeThreat = System.Convert.ToSingle(ht);
            if (state.TryGetValue("population", out var pop))
                ConstraintField.Population = System.Convert.ToInt32(pop);
            if (state.TryGetValue("currency_trust", out var ct))
                ConstraintField.CurrencyTrust = System.Convert.ToSingle(ct);
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"Multiplayer: Failed to parse world state: {e.Message}");
        }
    }

    // ─── State Sync Tick ───
    private float _syncTimer = 0f;
    private const float SyncInterval = 0.1f; // 10 Hz state sync

    public override void _Process(double delta)
    {
        if (!IsServer) return;

        _syncTimer += (float)delta;
        if (_syncTimer >= SyncInterval)
        {
            _syncTimer = 0f;
            // Reliable sync every 10 ticks
            Rpc(nameof(ReceiveWorldState), SerializeWorldState());
        }
    }

    // ─── Player Ready State ───
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SetPlayerReady(bool ready)
    {
        int sender = Multiplayer.GetRemoteSenderId();
        if (_players.TryGetValue(sender, out var info))
        {
            info.Ready = ready;
            _players[sender] = info;
        }
    }

    /// <summary>
    /// Check if all players are ready.
    /// </summary>
    public bool AllPlayersReady()
    {
        foreach (var kvp in _players)
        {
            if (!kvp.Value.Ready)
                return false;
        }
        return _players.Count > 0;
    }

    // ─── Utility ───
    private string GetPlayerName()
    {
        // Read from OS username or config
        return System.Environment.UserName;
    }

    /// <summary>
    /// Send a chat message to all players.
    /// </summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    public void SendChatMessage(string message)
    {
        int sender = Multiplayer.GetRemoteSenderId();
        string senderName = "Unknown";
        if (_players.TryGetValue(sender, out var info))
            senderName = info.Name;

        string formatted = $"{senderName}: {message}";
        GD.Print($"Chat: {formatted}");

        // Echo to all clients (including sender for confirmation)
        Rpc(nameof(ReceiveChatMessage), formatted);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer)]
    private void ReceiveChatMessage(string message)
    {
        GD.Print($"Chat: {message}");
        // Emit a signal for UI to pick up
    }
}
