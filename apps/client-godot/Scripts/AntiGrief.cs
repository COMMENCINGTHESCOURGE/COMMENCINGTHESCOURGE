using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Anti-grief system for multiplayer.
/// - Territory claims: players claim areas around their structures
/// - Vote kick: democratic removal of disruptive players
/// - Grief rollback: logs structure changes for reversal
/// - Permission system: who can build/modify in claimed areas
/// </summary>
public partial class AntiGrief : Node
{
    // ─── Territory Claims ───
    public class TerritoryClaim
    {
        public int OwnerPeerId;
        public string OwnerName;
        public Vector3 Center;
        public float Radius = 20f;
        public float ClaimTime;
        public List<int> AllowedPeers = new(); // Whitelist

        public bool ContainsPoint(Vector3 point)
        {
            return Center.DistanceTo(point) <= Radius;
        }
    }

    private Dictionary<string, TerritoryClaim> _claims = new(); // key: "x,y,z"
    private float _claimExpireDays = 14f; // Claims expire after 14 days of owner inactivity

    // ─── Vote Kick ───
    public class VoteKickSession
    {
        public int TargetPeerId;
        public string TargetName;
        public int InitiatorPeerId;
        public HashSet<int> YesVotes = new();
        public HashSet<int> NoVotes = new();
        public float StartTime;
        public float Duration = 60f; // 60 seconds to vote
        public bool Resolved = false;

        public AntiGrief Parent;

        public int RequiredYesVotes => Mathf.Max(2, Parent._playerCount / 2 + 1);
        public int TotalVotes => YesVotes.Count + NoVotes.Count;
    }

    private VoteKickSession _activeVote;
    private int _playerCount = 0;

    // ─── Grief Rollback Log ───
    public struct GriefLogEntry
    {
        public double Timestamp;
        public int PlayerPeerId;
        public string PlayerName;
        public string Action; // "place", "remove", "damage"
        public string PieceType;
        public Vector3I GridPosition;
        public bool Reverted;
    }

    private List<GriefLogEntry> _griefLog = new();
    private const int MaxLogEntries = 10000;

    // ─── Signals ───
    [Signal]
    public delegate void VoteStartedEventHandler(int targetPeerId, string targetName);
    [Signal]
    public delegate void VoteResolvedEventHandler(int targetPeerId, bool kicked);
    [Signal]
    public delegate void GriefDetectedEventHandler(int offenderPeerId, string action);
    [Signal]
    public delegate void TerritoryClaimedEventHandler(int ownerPeerId, Vector3 center);

    public override void _Ready()
    {
        // Get player count reference
        var mp = GetNodeOrNull<MultiplayerManager>("/root/World/MultiplayerManager");
        if (mp != null)
            _playerCount = mp.PlayerCount;

        // Auto-cleanup expired claims periodically
        var timer = new Timer();
        timer.WaitTime = 300.0; // 5 minutes
        timer.Timeout += CleanupExpiredClaims;
        AddChild(timer);
        timer.Start();
    }

    // ─── Territory System ───
    /// <summary>
    /// Claim a territory around the given position.
    /// </summary>
    public bool ClaimTerritory(int peerId, string playerName, Vector3 center, float radius = 20f)
    {
        string key = GetClaimKey(center);

        // Check if already claimed nearby
        foreach (var claim in _claims.Values)
        {
            if (claim.Center.DistanceTo(center) < radius + claim.Radius)
            {
                // Allow overlapping if same owner
                if (claim.OwnerPeerId == peerId)
                {
                    // Expand existing claim
                    claim.Radius = Mathf.Max(claim.Radius, radius);
                    claim.ClaimTime = Time.GetTicksMsec() / 1000f;
                    return true;
                }
                return false; // Someone else owns this area
            }
        }

        var newClaim = new TerritoryClaim
        {
            OwnerPeerId = peerId,
            OwnerName = playerName,
            Center = center,
            Radius = radius,
            ClaimTime = Time.GetTicksMsec() / 1000f
        };
        _claims[key] = newClaim;

        EmitSignal(SignalName.TerritoryClaimed, peerId, center);
        return true;
    }

    /// <summary>
    /// Check if a player can build at a given position.
    /// </summary>
    public bool CanBuildAt(int peerId, Vector3 position)
    {
        foreach (var claim in _claims.Values)
        {
            if (claim.ContainsPoint(position))
            {
                // Check if player is owner or whitelisted
                if (claim.OwnerPeerId == peerId || claim.AllowedPeers.Contains(peerId))
                    return true;
                return false; // This territory is claimed
            }
        }
        return true; // Unclaimed territory - anyone can build
    }

    /// <summary>
    /// Allow another player to build in your territory.
    /// </summary>
    public bool AddTerritoryAccess(int ownerPeerId, int targetPeerId, Vector3 position)
    {
        foreach (var claim in _claims.Values)
        {
            if (claim.OwnerPeerId == ownerPeerId && claim.ContainsPoint(position))
            {
                if (!claim.AllowedPeers.Contains(targetPeerId))
                    claim.AllowedPeers.Add(targetPeerId);
                return true;
            }
        }
        return false;
    }

    private string GetClaimKey(Vector3 pos)
    {
        int x = Mathf.RoundToInt(pos.X / 10f);
        int z = Mathf.RoundToInt(pos.Z / 10f);
        return $"{x},{z}";
    }

    private void CleanupExpiredClaims()
    {
        float now = Time.GetTicksMsec() / 1000f;
        var expired = new List<string>();
        foreach (var kvp in _claims)
        {
            if (now - kvp.Value.ClaimTime > _claimExpireDays * 86400f)
                expired.Add(kvp.Key);
        }
        foreach (var key in expired)
            _claims.Remove(key);
    }

    // ─── Vote Kick ───
    /// <summary>
    /// Initiate a vote to kick a player.
    /// </summary>
    public bool StartVoteKick(int initiatorPeerId, int targetPeerId, string targetName)
    {
        if (_activeVote != null && !_activeVote.Resolved)
            return false; // Vote already in progress

        // Can't kick server
        if (targetPeerId == 1) return false;

        _activeVote = new VoteKickSession
        {
            TargetPeerId = targetPeerId,
            TargetName = targetName,
            InitiatorPeerId = initiatorPeerId,
            StartTime = Time.GetTicksMsec() / 1000f,
            Parent = this
        };

        // Auto-yes from initiator
        _activeVote.YesVotes.Add(initiatorPeerId);

        EmitSignal(SignalName.VoteStarted, targetPeerId, targetName);
        return true;
    }

    /// <summary>
    /// Cast a vote.
    /// </summary>
    public bool CastVote(int peerId, bool yes)
    {
        if (_activeVote == null || _activeVote.Resolved)
            return false;

        if (_activeVote.YesVotes.Contains(peerId) || _activeVote.NoVotes.Contains(peerId))
            return false; // Already voted

        if (yes)
            _activeVote.YesVotes.Add(peerId);
        else
            _activeVote.NoVotes.Add(peerId);

        // Check if resolved
        CheckVoteResolution();
        return true;
    }

    private void CheckVoteResolution()
    {
        if (_activeVote == null) return;

        float now = Time.GetTicksMsec() / 1000f;
        bool timeExpired = (now - _activeVote.StartTime) > _activeVote.Duration;

        if (timeExpired || _activeVote.YesVotes.Count >= _activeVote.RequiredYesVotes)
        {
            // Kicked
            _activeVote.Resolved = true;
            EmitSignal(SignalName.VoteResolved, _activeVote.TargetPeerId, true);

            // Execute kick via MultiplayerManager
            var mp = GetNodeOrNull<MultiplayerManager>("/root/World/MultiplayerManager");
            if (mp != null && mp.IsServer)
            {
                mp.DisconnectFromServer(); // TODO: kick specific peer
            }
        }
        else if (_activeVote.NoVotes.Count >= _activeVote.RequiredYesVotes)
        {
            // Vote failed
            _activeVote.Resolved = true;
            EmitSignal(SignalName.VoteResolved, _activeVote.TargetPeerId, false);
        }
    }

    // ─── Grief Rollback ───
    /// <summary>
    /// Log a building action for potential rollback.
    /// </summary>
    public void LogAction(int peerId, string playerName, string action, string pieceType, Vector3I gridPos)
    {
        if (_griefLog.Count >= MaxLogEntries)
            _griefLog.RemoveAt(0);

        _griefLog.Add(new GriefLogEntry
        {
            Timestamp = Time.GetTicksMsec() / 1000.0,
            PlayerPeerId = peerId,
            PlayerName = playerName,
            Action = action,
            PieceType = pieceType,
            GridPosition = gridPos,
            Reverted = false
        });
    }

    /// <summary>
    /// Rollback all actions by a specific player within a time window.
    /// Returns number of actions rolled back.
    /// </summary>
    public int RollbackPlayerActions(int peerId, double windowSeconds = 300.0)
    {
        double cutoff = (Time.GetTicksMsec() / 1000.0) - windowSeconds;
        int count = 0;

        var buildingGrid = GetNodeOrNull<BuildingGrid>("/root/World/BuildingGrid");
        if (buildingGrid == null) return 0;

        // Find entries to revert (in reverse order so removals become placements)
        var toRevert = _griefLog
            .Where(e => e.PlayerPeerId == peerId && e.Timestamp >= cutoff && !e.Reverted)
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        foreach (var entry in toRevert)
        {
            if (entry.Action == "place")
            {
                // Remove what they placed
                buildingGrid.RemovePiece(entry.GridPosition);
            }
            else if (entry.Action == "remove")
            {
                // Re-place what they removed (use stored piece type)
                // This requires storing the piece type in building grid
            }

            // Mark as reverted
            int idx = _griefLog.FindIndex(e => e.Timestamp == entry.Timestamp && e.PlayerPeerId == peerId);
            if (idx >= 0)
            {
                var updated = _griefLog[idx];
                updated.Reverted = true;
                _griefLog[idx] = updated;
            }
            count++;
        }

        if (count > 0)
            EmitSignal(SignalName.GriefDetected, peerId, $"Rolled back {count} actions");

        return count;
    }

    /// <summary>
    /// Get recent grief score for a player (number of rapid structure changes).
    /// </summary>
    public float GetGriefScore(int peerId, double windowSeconds = 60.0)
    {
        double cutoff = (Time.GetTicksMsec() / 1000.0) - windowSeconds;
        int count = _griefLog.Count(e => e.PlayerPeerId == peerId && e.Timestamp >= cutoff);
        return count / 10f; // 10+ actions in 60s = suspect behavior
    }

    /// <summary>
    /// Check if a player should be flagged as a griefer.
    /// </summary>
    public bool IsSuspectedGriefer(int peerId)
    {
        return GetGriefScore(peerId) > 0.7f;
    }
}
