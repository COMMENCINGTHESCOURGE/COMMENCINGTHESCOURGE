using Godot;

/// <summary>
/// Spawns zombies near the player based on ConstraintField horde threat and time of day.
/// </summary>
public partial class ZombieSpawner : Node
{
    [Export] public PackedScene ZombieScene;
    [Export] public float SpawnRadius = 30.0f;
    [Export] public int MaxZombies = 20;
    [Export] public float SpawnInterval = 5.0f;

    private Node3D _player;
    private float _timer = 0.0f;

    public override void _Ready()
    {
        _player = (Node3D)GetTree().GetFirstNodeInGroup("Player");
        if (ZombieScene == null)
            GD.PrintErr("ZombieSpawner: No ZombieScene assigned!");
    }

    public override void _Process(double delta)
    {
        if (_player == null || ZombieScene == null) return;

        _timer += (float)delta;

        // Count existing zombies
        var zombies = GetTree().GetNodesInGroup("Zombies");
        int currentCount = zombies.Count;

        if (_timer >= SpawnInterval && currentCount < MaxZombies)
        {
            _timer = 0.0f;

            // Spawn chance based on horde threat and time of day
            float spawnChance = ConstraintField.HordeThreat * 0.5f;
            bool isNight = ConstraintField.TimeOfDay < 6.0f || ConstraintField.TimeOfDay > 20.0f;
            if (isNight) spawnChance += 0.3f;

            if (GD.Randf() < spawnChance)
            {
                SpawnZombie();
            }
        }
    }

    private void SpawnZombie()
    {
        float angle = (float)GD.RandRange(0, Mathf.Pi * 2);
        float dist = SpawnRadius + (float)GD.RandRange(0, 10);
        Vector3 pos = _player.GlobalPosition + new Vector3(
            Mathf.Cos(angle) * dist,
            2.0f,
            Mathf.Sin(angle) * dist
        );

        var zombie = ZombieScene.Instantiate<Zombie>();
        zombie.Position = pos;
        zombie.AddToGroup("Zombies");

        // Random tier based on horde threat
        float roll = GD.Randf();
        if (roll < 0.5f) zombie.Tier = Zombie.ZombieTier.Walker;
        else if (roll < 0.75f) zombie.Tier = Zombie.ZombieTier.Runner;
        else if (roll < 0.9f) zombie.Tier = Zombie.ZombieTier.Brute;
        else zombie.Tier = Zombie.ZombieTier.Spitter;

        AddChild(zombie);

        ConstraintField.ZombieCount++;
    }
}
