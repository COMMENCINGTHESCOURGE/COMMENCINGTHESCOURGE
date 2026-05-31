using Godot;

/// <summary>
/// Phase 20: The Metric Foundation
/// Implements "The Foundation Developer" (Geometric Constants) 
/// and "The Stair Counter" (Pacing Calculation).
/// </summary>
public partial class MetricArchitect : Node
{
    public static MetricArchitect Instance { get; private set; }

    // Foundation Constants
    public const float StandardFloorHeight = 3.0f;
    public const float BiLevelSplitHeight = 1.5f;
    public const float StairTreadDepth = 0.3f;
    public const float StairRiserHeight = 0.15f;

    public override void _Ready()
    {
        Instance = this;
        // Optionally generate a visual block to represent the Bi-Level geometry
        GenerateFoundationBlocks();
    }

    private void GenerateFoundationBlocks()
    {
        // Procedurally generating a Bi-Level Foundation representation
        var root = GetTree().Root;
        
        var mainFloor = new CsgBox3D();
        mainFloor.Size = new Vector3(10, 0.5f, 10);
        mainFloor.Position = new Vector3(20, 0, 20); // Placed off to the side
        mainFloor.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.3f, 0.3f) };
        
        var splitFloor = new CsgBox3D();
        splitFloor.Size = new Vector3(10, 0.5f, 5);
        splitFloor.Position = new Vector3(20, BiLevelSplitHeight, 27.5f); // 1.5m up
        splitFloor.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.4f, 0.4f) };

        // Phase 21: Add Property Integrity
        var integrity1 = new PropertyIntegrity();
        mainFloor.AddChild(integrity1);
        
        var integrity2 = new PropertyIntegrity();
        splitFloor.AddChild(integrity2);

        root.CallDeferred("add_child", mainFloor);
        root.CallDeferred("add_child", splitFloor);
    }

    /// <summary>
    /// The Stair Counter logic. Calculates exact traversal time for a shot.
    /// Accounts for vertical traversal penalties (stairs).
    /// </summary>
    public float CalculatePacing(Vector3 start, Vector3 end, float speed)
    {
        float horizontalDistance = new Vector2(end.X - start.X, end.Z - start.Z).Length();
        float verticalDistance = Mathf.Abs(end.Y - start.Y);

        // Standard walk time for flat ground
        float time = horizontalDistance / speed;

        // If there's a vertical difference, the "Stair Counter" calculates the penalty.
        // E.g., walking up stairs is slower. Let's add a 40% time penalty for vertical climb.
        if (verticalDistance > 0.1f)
        {
            float stairTime = verticalDistance / (speed * 0.6f); 
            time += stairTime;
        }

        return time;
    }
}
