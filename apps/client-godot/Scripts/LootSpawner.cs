using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Phase 10: Randomized Loot Extrusion
/// Randomly selects products from the ItemDatabase and spawns them into the world as interactable RigidBody3D objects.
/// </summary>
public partial class LootSpawner : Node3D
{
    [Export] public float SpawnInterval = 10.0f;
    [Export] public float SpawnRadius = 15.0f;
    [Export] public int MaxLootItems = 20;

    private float _timer = 0.0f;
    private Random _rng = new Random();
    private List<Node> _spawnedLoot = new List<Node>();

    public override void _Process(double delta)
    {
        // Ensure database is loaded
        if (ItemDatabase.Instance == null || !ItemDatabase.Instance.AllItems().Any()) return;

        _timer += (float)delta;
        if (_timer >= SpawnInterval)
        {
            _timer = 0.0f;
            CleanUpLoot();

            if (_spawnedLoot.Count < MaxLootItems)
            {
                SpawnRandomLoot();
            }
        }
    }

    private void SpawnRandomLoot()
    {
        var allItems = ItemDatabase.Instance.AllItems().ToList();
        var item = allItems[_rng.Next(allItems.Count)];

        // Generate a random position within radius
        float angle = (float)_rng.NextDouble() * Mathf.Pi * 2.0f;
        float dist = (float)_rng.NextDouble() * SpawnRadius;
        Vector3 spawnPos = GlobalPosition + new Vector3(Mathf.Cos(angle) * dist, 5.0f, Mathf.Sin(angle) * dist);

        // Create RigidBody representation
        var lootBody = new RigidBody3D();
        lootBody.Position = spawnPos;
        
        // Tag it so InteractionSystem can recognize it
        lootBody.SetMeta("is_loot", true);
        lootBody.SetMeta("item_id", item.Id);
        lootBody.SetMeta("item_name", item.Name);

        // Add visual mesh (a generic box for now, tinted based on category)
        var meshInstance = new MeshInstance3D();
        var boxMesh = new BoxMesh { Size = new Vector3(0.4f, 0.4f, 0.4f) };
        var material = new StandardMaterial3D();
        
        // Eccentric novelty items get a shiny gold color, others get generic brown
        if (item.Category == "novelty")
        {
            material.AlbedoColor = new Color(1.0f, 0.8f, 0.1f);
            material.Metallic = 1.0f;
            material.Roughness = 0.2f;
        }
        else
        {
            material.AlbedoColor = new Color(0.4f, 0.3f, 0.2f);
        }

        boxMesh.Material = material;
        meshInstance.Mesh = boxMesh;
        lootBody.AddChild(meshInstance);

        // Add collision
        var collisionShape = new CollisionShape3D();
        collisionShape.Shape = new BoxShape3D { Size = new Vector3(0.4f, 0.4f, 0.4f) };
        lootBody.AddChild(collisionShape);

        // Area for player proximity detection
        var area = new Area3D();
        var areaShape = new CollisionShape3D();
        areaShape.Shape = new SphereShape3D { Radius = 1.5f };
        area.AddChild(areaShape);
        area.BodyEntered += (Node3D body) => OnLootAreaEntered(body, lootBody, item);
        lootBody.AddChild(area);

        GetTree().Root.AddChild(lootBody);
        _spawnedLoot.Add(lootBody);

        GD.Print($"LootSpawner: Dropped {item.Name} at {spawnPos}");
    }

    private void OnLootAreaEntered(Node3D body, RigidBody3D lootBody, ItemDef item)
    {
        if (body.IsInGroup("Player") && IsInstanceValid(lootBody))
        {
            // Player collects the item
            GD.Print($"*** PLAYER COLLECTED: {item.Name} ***");
            if (item.Category == "novelty")
            {
                ConstraintField.Morale = Mathf.Clamp(ConstraintField.Morale + 0.1f, 0.0f, 1.0f);
            }
            
            _spawnedLoot.Remove(lootBody);
            lootBody.QueueFree();
        }
    }

    private void CleanUpLoot()
    {
        _spawnedLoot.RemoveAll(node => !IsInstanceValid(node));
    }
}
