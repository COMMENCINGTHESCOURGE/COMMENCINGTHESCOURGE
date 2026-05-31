using Godot;
using System.Collections.Generic;

/// <summary>
/// Road, bridge, and tunnel network generator.
/// Connects buildings, provides vehicle routing, enables travel between chunks.
/// 
/// Three layers:
///   1. Primary roads (4-lane, connects districts) — asphalt, cleared, zombie patrol routes
///   2. Secondary roads (2-lane, connects blocks) — asphalt/concrete, some debris
///   3. Tertiary roads (1-lane, alleys, driveways) — gravel/dirt, choked with wrecks
/// 
/// Bridges cross water/valleys. Tunnels go through terrain or under buildings.
/// Basements connect to building footers. Sub-basements are deeper, rarer.
/// </summary>
public partial class RoadNetwork : Node3D
{
    public static RoadNetwork Instance { get; private set; }
    
    // The "local channel" for calling road corrections
    public event System.Action<Vector2> OnRoadRepairRequested;

    [Export] public float PrimaryWidth = 8.0f;    // 4 lanes
    [Export] public float SecondaryWidth = 5.0f;  // 2 lanes
    [Export] public float TertiaryWidth = 2.5f;   // 1 lane
    [Export] public float BridgeWidth = 6.0f;
    [Export] public float TunnelHeight = 4.0f;
    [Export] public float TunnelWidth = 5.0f;

    // ─── Road Segments ───
    public enum RoadType
    {
        Primary,
        Secondary,
        Tertiary,
        Bridge,
        Tunnel
    }

    public enum ConnectionType
    {
        Intersection,
        DeadEnd,
        Roundabout
    }

    public class RoadSegment
    {
        public Vector2 Start;
        public Vector2 End;
        public RoadType Type;
        public float Width;
        public float Integrity = 1.0f;
        public MeshInstance3D VisualMesh;
        public bool HasBarricade = false;
        public bool IsBlocked = false;        // debris/vehicle wreck
        public List<string> DebrisItems = new(); // what's blocking it

        public float Length => Start.DistanceTo(End);
        public Vector2 Direction => (End - Start).Normalized();
    }

    public class BridgeSegment : RoadSegment
    {
        public float SpanLength;        // distance between supports
        public float ClearanceBelow;    // for boats
        public bool HasPierSupport = true;
        public float DeckHealth = 200f; // bridge-specific health
    }

    public class TunnelSegment : RoadSegment
    {
        public float DepthBelowGrade;   // how far underground
        public bool HasLights = false;
        public bool HasVentilation = false;
        public bool IsFlooded = false;
        public float FloodLevel = 0f;   // 0 = dry, 1 = fully flooded
        public HashSet<int> ConnectedBasements = new();
    }

    public class Intersection
    {
        public Vector2 Position;
        public ConnectionType Type;
        public List<RoadSegment> ConnectedRoads = new();
        public bool HasTrafficLights = false;
    }

    // ─── Storage ───
    private List<RoadSegment> _roads = new();
    private List<BridgeSegment> _bridges = new();
    private List<TunnelSegment> _tunnels = new();
    private Dictionary<Vector2I, Intersection> _intersections = new();

    // Materials
    private StandardMaterial3D _asphaltMat;
    private StandardMaterial3D _concreteMat;
    private StandardMaterial3D _gravelMat;
    private StandardMaterial3D _bridgeDeckMat;
    private StandardMaterial3D _tunnelMat;

    public override void _Ready()
    {
        Instance = this;
        CreateMaterials();

        // Subscribe to the local channel
        OnRoadRepairRequested += HandleRoadRepairRequest;
    }

    private void HandleRoadRepairRequest(Vector2 position)
    {
        // 1. Find the nearest road segment
        RoadSegment road = GetNearestRoad(position, out float dist);
        if (road != null && dist < 10f)
        {
            // 2. Correct the road (Fill the pothole)
            if (road.Integrity < 1.0f)
            {
                road.Integrity = 1.0f; // Instantly repair the structural matrix
                if (AmbientFXController.Instance != null)
                {
                    AmbientFXController.Instance.TriggerRoadRepair(new Vector3(position.X, 0f, position.Y));
                }
                GD.Print($"RoadNetwork: Local channel invoked. Pothole filled at {position}.");
            }
        }
    }

    // Call this to invoke the program
    public void RequestRoadRepair(Vector2 position)
    {
        OnRoadRepairRequested?.Invoke(position);
    }

    /// <summary>
    /// Phase 15: Kinematic Cascade
    /// Registers a temporary obstruction that drastically alters pathfinding weights.
    /// </summary>
    public void CreateAnchorNode(Vector2 position)
    {
        RoadSegment road = GetNearestRoad(position, out float dist);
        if (road != null && dist < 15f)
        {
            road.IsBlocked = true; // The constraint shifts. Traffic must now reroute.
            GD.Print($"RoadNetwork: Kinematic Anchor Node registered at {position}. Traffic will reroute.");
        }
    }

    private void CreateMaterials()
    {
        _asphaltMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.2f, 0.22f),
            Metallic = 0.0f,
            Roughness = 0.95f
        };
        _concreteMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.5f, 0.5f, 0.52f),
            Metallic = 0.0f,
            Roughness = 0.9f
        };
        _gravelMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.45f, 0.4f, 0.35f),
            Metallic = 0.0f,
            Roughness = 1.0f
        };
        _bridgeDeckMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.35f, 0.4f),
            Metallic = 0.05f,
            Roughness = 0.8f
        };
        _tunnelMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.15f, 0.15f, 0.18f),
            Metallic = 0.0f,
            Roughness = 0.9f
        };
    }

    /// <summary>
    /// Generate road network from a set of building positions.
    /// Connects buildings with appropriate road types based on density.
    /// </summary>
    public void GenerateNetwork(List<Vector2> buildingPositions, List<Vector2> terrainBlockers)
    {
        if (buildingPositions.Count < 2) return;

        // Build a minimum spanning tree between building clusters
        // Then overlay a grid pattern on high-density zones

        // Phase 1: Identify clusters (buildings within 50m of each other)
        var clusters = FindClusters(buildingPositions, 50f);

        // Phase 2: Connect clusters with primary roads
        for (int i = 0; i < clusters.Count - 1; i++)
        {
            var a = GetClusterCenter(clusters[i]);
            var b = GetClusterCenter(clusters[i + 1]);
            CreateRoad(a, b, RoadType.Primary, terrainBlockers);
        }

        // Phase 3: Local road grid within clusters (secondary)
        foreach (var cluster in clusters)
        {
            if (cluster.Count > 3)
            {
                var center = GetClusterCenter(cluster);
                // Place a grid pattern
                for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    Vector2 offset = new Vector2(x * 30f, z * 30f);
                    Vector2 nodeA = center + offset;
                    Vector2 nodeB = center + offset + new Vector2(30f, 0);
                    Vector2 nodeC = center + offset + new Vector2(0, 30f);
                    CreateRoad(nodeA, nodeB, RoadType.Secondary, terrainBlockers);
                    CreateRoad(nodeA, nodeC, RoadType.Secondary, terrainBlockers);
                }
            }
        }

        // Phase 4: Driveways to individual buildings (tertiary)
        foreach (var pos in buildingPositions)
        {
            float nearestDist = float.MaxValue;
            RoadSegment nearest = null;
            foreach (var road in _roads)
            {
                float dist = DistanceToSegment(pos, road.Start, road.End);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = road;
                }
            }
            if (nearest != null && nearestDist > 5f && nearestDist < 30f)
            {
                // Find nearest point on the road
                Vector2 nearestPoint = NearestPointOnSegment(pos, nearest.Start, nearest.End);
                CreateRoad(pos, nearestPoint, RoadType.Tertiary, terrainBlockers);
            }
        }
    }

    /// <summary>
    /// Create a road segment, automatically upgrading to bridge or tunnel based on terrain.
    /// </summary>
    public RoadSegment CreateRoad(Vector2 start, Vector2 end, RoadType preferredType, List<Vector2> terrainBlockers)
    {
        float length = start.DistanceTo(end);
        if (length < 2f) return null; // Too short

        // Check if this overlaps with existing roads near the endpoints
        Vector2I startKey = new Vector2I(Mathf.RoundToInt(start.X / 5f), Mathf.RoundToInt(start.Y / 5f));
        if (_intersections.ContainsKey(startKey))
            return null; // Already have a junction here

        // Check terrain blockers for bridge/tunnel decision
        bool needsBridge = false;
        bool needsTunnel = false;
        // Check midpoint for water/valley (simplified: if terrain blocking at midpoint)
        Vector2 mid = (start + end) / 2f;
        foreach (var blocker in terrainBlockers)
        {
            if (mid.DistanceTo(blocker) < 10f)
            {
                needsBridge = true;
                break;
            }
        }

        RoadType actualType = needsBridge ? RoadType.Bridge : needsTunnel ? RoadType.Tunnel : preferredType;
        float width = actualType switch
        {
            RoadType.Primary => PrimaryWidth,
            RoadType.Secondary => SecondaryWidth,
            RoadType.Tertiary => TertiaryWidth,
            RoadType.Bridge => BridgeWidth,
            RoadType.Tunnel => TunnelWidth,
            _ => SecondaryWidth
        };

        // Create the visual
        RoadSegment segment;
        if (actualType == RoadType.Bridge)
        {
            var bridge = new BridgeSegment
            {
                Start = start, End = end,
                Type = RoadType.Bridge,
                Width = width,
                SpanLength = length,
                ClearanceBelow = 3f
            };
            segment = bridge;
            _bridges.Add(bridge);
        }
        else if (actualType == RoadType.Tunnel)
        {
            var tunnel = new TunnelSegment
            {
                Start = start, End = end,
                Type = RoadType.Tunnel,
                Width = width,
                DepthBelowGrade = 5f,
                HasLights = false
            };
            segment = tunnel;
            _tunnels.Add(tunnel);
        }
        else
        {
            segment = new RoadSegment
            {
                Start = start, End = end,
                Type = actualType,
                Width = width
            };
        }

        // Instantiate visual
        var mesh = CreateRoadMesh(segment);
        segment.VisualMesh = mesh;
        AddChild(mesh);

        _roads.Add(segment);

        // Register intersections at endpoints
        RegisterIntersection(start, segment);
        RegisterIntersection(end, segment);

        return segment;
    }

    private MeshInstance3D CreateRoadMesh(RoadSegment segment)
    {
        var mesh = new MeshInstance3D();
        Vector3 start3 = new Vector3(segment.Start.X, 0.05f, segment.Start.Y);
        Vector3 end3 = new Vector3(segment.End.X, 0.05f, segment.End.Y);
        Vector3 mid = (start3 + end3) / 2f;
        float length = start3.DistanceTo(end3);
        float angle = Mathf.Atan2(end3.Z - start3.Z, end3.X - start3.X);

        // Road is a flat box aligned to the segment direction
        var box = new BoxMesh();
        box.Size = new Vector3(length, 0.1f, segment.Width);

        if (segment.Type == RoadType.Bridge)
        {
            box.Material = _bridgeDeckMat;
        }
        else if (segment.Type == RoadType.Tunnel)
        {
            box.Material = _tunnelMat;
        }
        else
        {
            box.Material = segment.Type == RoadType.Tertiary ? _gravelMat :
                           segment.Type == RoadType.Secondary ? _concreteMat : _asphaltMat;
        }

        mesh.Mesh = box;
        mesh.Position = new Vector3(mid.X, 0.05f, mid.Z);
        mesh.Rotation = new Vector3(0, -angle, 0);

        return mesh;
    }

    // ─── Utility ───
    private List<List<Vector2>> FindClusters(List<Vector2> points, float threshold)
    {
        var clusters = new List<List<Vector2>>();
        var visited = new HashSet<int>();
        for (int i = 0; i < points.Count; i++)
        {
            if (visited.Contains(i)) continue;
            var cluster = new List<Vector2> { points[i] };
            visited.Add(i);
            for (int j = i + 1; j < points.Count; j++)
            {
                if (visited.Contains(j)) continue;
                if (points[i].DistanceTo(points[j]) < threshold)
                {
                    cluster.Add(points[j]);
                    visited.Add(j);
                }
            }
            clusters.Add(cluster);
        }
        return clusters;
    }

    private Vector2 GetClusterCenter(List<Vector2> cluster)
    {
        Vector2 sum = Vector2.Zero;
        foreach (var p in cluster) sum += p;
        return sum / cluster.Count;
    }

    private void RegisterIntersection(Vector2 pos, RoadSegment road)
    {
        Vector2I key = new Vector2I(Mathf.RoundToInt(pos.X / 5f), Mathf.RoundToInt(pos.Y / 5f));
        if (!_intersections.ContainsKey(key))
        {
            _intersections[key] = new Intersection { Position = pos, Type = ConnectionType.Intersection };
        }
        _intersections[key].ConnectedRoads.Add(road);
    }

    private float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        Vector2 ap = p - a;
        float t = Mathf.Clamp(ap.Dot(ab) / ab.Dot(ab), 0f, 1f);
        Vector2 closest = a + ab * t;
        return p.DistanceTo(closest);
    }

    private Vector2 NearestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        Vector2 ap = p - a;
        float t = Mathf.Clamp(ap.Dot(ab) / ab.Dot(ab), 0f, 1f);
        return a + ab * t;
    }

    // ─── Public API ───
    public List<RoadSegment> GetRoadsInRadius(Vector2 center, float radius)
    {
        var result = new List<RoadSegment>();
        foreach (var road in _roads)
        {
            float dist = DistanceToSegment(center, road.Start, road.End);
            if (dist < radius) result.Add(road);
        }
        return result;
    }

    public RoadSegment GetNearestRoad(Vector2 position, out float distance)
    {
        RoadSegment nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var road in _roads)
        {
            float dist = DistanceToSegment(position, road.Start, road.End);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = road;
            }
        }
        distance = nearestDist;
        return nearest;
    }

    public bool HasPath(Vector2 from, Vector2 to, float maxDetour = 200f)
    {
        // Simple check: is there a road segment within maxDetour of both points?
        var roadFrom = GetNearestRoad(from, out float distFrom);
        var roadTo = GetNearestRoad(to, out float distTo);
        if (roadFrom == null || roadTo == null) return false;
        return distFrom < maxDetour && distTo < maxDetour;
    }

    public float GetNetworkConnectivity()
    {
        // What percentage of buildings are within 30m of a road?
        // (Stub — requires building position list to be stored)
        return _roads.Count > 0 ? 0.8f : 0f;
    }

    /// <summary>
    /// Apply degradation to all roads, bridges, and tunnels.
    /// Bridges decay fastest (exposed). Tunnels slowest (protected).
    /// </summary>
    public void ApplyDegradation(float rate)
    {
        foreach (var road in _roads)
        {
            if (road is BridgeSegment bridge)
            {
                bridge.DeckHealth -= rate * 0.003f;
                bridge.Integrity = bridge.DeckHealth / 200f;
                if (bridge.DeckHealth <= 0)
                    CollapseBridge(bridge);
            }
            else if (road is TunnelSegment tunnel)
            {
                tunnel.Integrity -= rate * 0.0005f; // Tunnels degrade very slowly
                if (tunnel.FloodLevel > 0.5f)
                    tunnel.Integrity -= rate * 0.002f; // Flooding accelerates tunnel decay
            }
            else
            {
                road.Integrity -= rate * 0.001f;
            }
        }
    }

    private void CollapseBridge(BridgeSegment bridge)
    {
        if (bridge.VisualMesh != null)
            bridge.VisualMesh.QueueFree();
        bridge.IsBlocked = true;
        bridge.DebrisItems.Add("bridge_ruins");
    }

    /// <summary>
    /// Clear a blocked road (remove debris/wreck).
    /// </summary>
    public bool ClearBlockage(RoadSegment segment)
    {
        if (!segment.IsBlocked) return false;
        segment.IsBlocked = false;
        segment.DebrisItems.Clear();
        return true;
    }
}
