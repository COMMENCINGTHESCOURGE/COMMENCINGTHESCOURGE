using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// World chunk streamer. Loads/unloads chunks around the player.
/// Supports priority loading based on player velocity/direction.
/// </summary>
public partial class WorldStreamer : Node
{
    [Export] public int ChunkSize = 100;           // meters per chunk
    [Export] public int LoadRadius = 3;             // chunks to load in each direction
    [Export] public int UnloadRadius = 4;           // chunks to keep before unloading
    [Export] public PackedScene ChunkTemplate;      // chunk scene to instance

    private Dictionary<Vector2I, Node3D> _loadedChunks = new();
    private CharacterBody3D _player;

    public override void _Ready()
    {
        _player = (CharacterBody3D)GetTree().GetFirstNodeInGroup("Player");
        if (ChunkTemplate == null)
            GD.PrintErr("WorldStreamer: No ChunkTemplate assigned!");
    }

    public override void _Process(double delta)
    {
        if (_player == null) return;

        Vector2I playerChunk = GetChunkPos(_player.GlobalPosition);
        Vector3 playerVelocity = _player.Velocity;
        Vector2I velocityDir = new(
            (int)Mathf.Sign(playerVelocity.X),
            (int)Mathf.Sign(playerVelocity.Z)
        );

        // Build priority map: chunks in movement direction load first
        HashSet<Vector2I> needed = new();
        for (int x = -LoadRadius; x <= LoadRadius; x++)
        {
            for (int z = -LoadRadius; z <= LoadRadius; z++)
            {
                Vector2I chunk = new(playerChunk.X + x, playerChunk.Y + z);
                needed.Add(chunk);
            }
        }

        // Sort: chunks in movement direction get priority
        var neededSorted = needed
            .OrderByDescending(c =>
            {
                Vector2I offset = new(c.X - playerChunk.X, c.Y - playerChunk.Y);
                float dot = offset.X * velocityDir.X + offset.Y * velocityDir.Y;
                return dot;
            })
            .ToList();

        // Unload chunks outside unload radius
        var toUnload = new List<Vector2I>();
        foreach (var chunk in _loadedChunks.Keys)
        {
            int dist = Mathf.Max(
                Mathf.Abs(chunk.X - playerChunk.X),
                Mathf.Abs(chunk.Y - playerChunk.Y)
            );
            if (dist > UnloadRadius)
                toUnload.Add(chunk);
        }
        foreach (var chunk in toUnload)
            UnloadChunk(chunk);

        // Load missing chunks
        foreach (var chunk in neededSorted)
        {
            if (!_loadedChunks.ContainsKey(chunk))
                LoadChunk(chunk);
        }
    }

    private Vector2I GetChunkPos(Vector3 worldPos)
    {
        return new Vector2I(
            (int)Mathf.Floor(worldPos.X / ChunkSize),
            (int)Mathf.Floor(worldPos.Z / ChunkSize)
        );
    }

    private void LoadChunk(Vector2I pos)
    {
        if (ChunkTemplate == null)
        {
            // Fallback: generate a flat ground chunk procedurally
            var chunk = new Node3D();
            chunk.Name = $"Chunk_{pos.X}_{pos.Y}";
            chunk.Position = new Vector3(pos.X * ChunkSize, 0, pos.Y * ChunkSize);

            // Add a ground plane
            var mesh = new MeshInstance3D();
            mesh.Mesh = new BoxMesh
            {
                Size = new Vector3(ChunkSize, 1, ChunkSize),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.3f, 0.5f, 0.2f)
                }
            };
            mesh.Position = new Vector3(ChunkSize / 2.0f, -0.5f, ChunkSize / 2.0f);
            chunk.AddChild(mesh);

            // Add a static body for collision
            var body = new StaticBody3D();
            var shape = new CollisionShape3D();
            shape.Shape = new BoxShape3D { Size = new Vector3(ChunkSize, 1, ChunkSize) };
            body.AddChild(shape);
            body.Position = new Vector3(ChunkSize / 2.0f, -0.5f, ChunkSize / 2.0f);
            chunk.AddChild(body);

            AddChild(chunk);
            _loadedChunks[pos] = chunk;
        }
        else
        {
            var chunk = ChunkTemplate.Instantiate<Node3D>();
            chunk.Name = $"Chunk_{pos.X}_{pos.Y}";
            chunk.Position = new Vector3(pos.X * ChunkSize, 0, pos.Y * ChunkSize);
            AddChild(chunk);
            _loadedChunks[pos] = chunk;
        }
    }

    private void UnloadChunk(Vector2I pos)
    {
        if (_loadedChunks.TryGetValue(pos, out var chunk))
        {
            chunk.QueueFree();
            _loadedChunks.Remove(pos);
        }
    }

    /// <summary>
    /// Get the number of currently loaded chunks.
    /// </summary>
    public int LoadedChunkCount => _loadedChunks.Count;
}
