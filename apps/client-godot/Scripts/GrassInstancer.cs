using Godot;

/// <summary>
/// Phase 27: The Material Tensor Pipeline
/// Instancer for the wind vegetation shader. Spawns 10,000 instances of grass.
/// </summary>
public partial class GrassInstancer : MultiMeshInstance3D
{
    [Export] public int GrassCount = 10000;
    [Export] public float SpawnRadius = 105.0f; // 15 to 105 to match the radius math in Filament (15 + 90)

    private const int MATERIAL_COUNT = 256;
    private ImageTexture _tensorTexture;
    
    // Phase 29: GPU Compute Culling
    private ComputeCuller _culler;
    private ArchitectAI _architectAI;
    private float[] _rawInstanceData;
    private bool _cullingEnabled = true;

    public override void _Ready()
    {
        // Ensure we have a MultiMesh
        if (Multimesh == null)
        {
            Multimesh = new MultiMesh();
            Multimesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
            Multimesh.UseCustomData = true; // Crucial for INSTANCE_CUSTOM
            Multimesh.InstanceCount = GrassCount;
            
            // Create a simple grass blade mesh programmatically if one isn't assigned
            if (Multimesh.Mesh == null)
            {
                Multimesh.Mesh = CreateGrassMesh();
            }
        }
        else
        {
            Multimesh.UseCustomData = true;
            Multimesh.InstanceCount = GrassCount;
        }

        GenerateMaterialTensor();
        PopulateInstances();

        // Phase 29: Initialize Compute Culler
        _culler = new ComputeCuller();
        AddChild(_culler);
        _culler.Initialize(_rawInstanceData, GrassCount, _tensorTexture);
        
        // Phase 30: Initialize Architect AI
        _architectAI = new ArchitectAI();
        AddChild(_architectAI);
    }

    public override void _Process(double delta)
    {
        if (_cullingEnabled && _culler != null)
        {
            // Dispatch compute shader
            // For grass, LOD distance might be 60.0f
            float[] compactedData = _culler.DispatchCull(60.0f);
            
            if (compactedData != null)
            {
                // Phase 30: Pass to Architect
                if (_architectAI != null)
                {
                    _architectAI.AnalyzeCullingFeedback(_rawInstanceData, compactedData);
                }

                // Each instance is 16 floats (12 for transform, 4 for custom data)
                int visibleCount = compactedData.Length / 16;
                Multimesh.InstanceCount = visibleCount;
                
                for (int i = 0; i < visibleCount; i++)
                {
                    int offset = i * 16;
                    
                    // Column-major Transform3D from flat array
                    Transform3D t = new Transform3D(
                        new Vector3(compactedData[offset+0], compactedData[offset+1], compactedData[offset+2]),
                        new Vector3(compactedData[offset+4], compactedData[offset+5], compactedData[offset+6]),
                        new Vector3(compactedData[offset+8], compactedData[offset+9], compactedData[offset+10]),
                        new Vector3(compactedData[offset+12], compactedData[offset+13], compactedData[offset+14])
                    );
                    
                    Color customData = new Color(
                        compactedData[offset+3],
                        compactedData[offset+7],
                        compactedData[offset+11],
                        compactedData[offset+15]
                    );

                    Multimesh.SetInstanceTransform(i, t);
                    Multimesh.SetInstanceCustomData(i, customData);
                }
            }
        }
    }

    private void GenerateMaterialTensor()
    {
        Image img = Image.CreateEmpty(MATERIAL_COUNT, 1, false, Image.Format.Rgba8);
        for (int i = 0; i < MATERIAL_COUNT; i++)
        {
            float t = (float)i / MATERIAL_COUNT;
            float cohesion = t; // 0->1
            float yield = 1.0f - t; // 1->0
            float moisture = 0.5f + t * 0.5f; // 0.5->1.0
            float density = 1.0f;
            
            img.SetPixel(i, 0, new Color(cohesion, yield, moisture, density));
        }
        
        _tensorTexture = ImageTexture.CreateFromImage(img);

        // Assign to material if possible
        if (MaterialOverride is ShaderMaterial shaderMat)
        {
            shaderMat.SetShaderParameter("materialTensor", _tensorTexture);
        }
        else if (Multimesh.Mesh != null && Multimesh.Mesh.GetSurfaceCount() > 0 && Multimesh.Mesh.SurfaceGetMaterial(0) is ShaderMaterial meshMat)
        {
            meshMat.SetShaderParameter("materialTensor", _tensorTexture);
        }
    }

    private void PopulateInstances()
    {
        _rawInstanceData = new float[GrassCount * 16];

        for (int i = 0; i < GrassCount; i++)
        {
            // Match the Filament demo distribution logic
            float angle = (float)GD.RandRange(0, Mathf.Pi * 2.0);
            float radius = 15.0f + (float)GD.RandRange(0, SpawnRadius - 15.0f);
            
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            float y = 0.0f; // Could be sampled from terrain heightmap later

            // Phase 28: Assign a random material index (0 to 255)
            float materialIdx = (float)GD.RandRange(0, MATERIAL_COUNT - 1);

            Vector3 worldPos = GlobalPosition + new Vector3(x, y, z);
            
            Transform3D transform = new Transform3D(Basis.Identity, new Vector3(x, y, z));
            Multimesh.SetInstanceTransform(i, transform);
            
            Color customData = new Color(worldPos.X, worldPos.Y, worldPos.Z, materialIdx);
            Multimesh.SetInstanceCustomData(i, customData);

            // Phase 29: Store raw data for compute shader
            int offset = i * 16;
            
            // vec4 transform_0
            _rawInstanceData[offset+0] = transform.Basis.X.X;
            _rawInstanceData[offset+1] = transform.Basis.X.Y;
            _rawInstanceData[offset+2] = transform.Basis.X.Z;
            _rawInstanceData[offset+3] = customData.R;
            
            // vec4 transform_1
            _rawInstanceData[offset+4] = transform.Basis.Y.X;
            _rawInstanceData[offset+5] = transform.Basis.Y.Y;
            _rawInstanceData[offset+6] = transform.Basis.Y.Z;
            _rawInstanceData[offset+7] = customData.G;
            
            // vec4 transform_2
            _rawInstanceData[offset+8] = transform.Basis.Z.X;
            _rawInstanceData[offset+9] = transform.Basis.Z.Y;
            _rawInstanceData[offset+10] = transform.Basis.Z.Z;
            _rawInstanceData[offset+11] = customData.B;
            
            // vec4 custom_data
            _rawInstanceData[offset+12] = transform.Origin.X;
            _rawInstanceData[offset+13] = transform.Origin.Y;
            _rawInstanceData[offset+14] = transform.Origin.Z;
            _rawInstanceData[offset+15] = customData.A;
        }
        GD.Print($"[TENSOR] Spawned {GrassCount} vegetation instances with Tensor bound.");
    }

    private Mesh CreateGrassMesh()
    {
        // Simple Quad/Blade fallback
        ArrayMesh arrMesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        Vector3[] verts = new Vector3[3];
        verts[0] = new Vector3(0.0f, 0.35f, 0.0f);   // Apex
        verts[1] = new Vector3(-0.04f, 0.0f, 0.0f);  // Base left
        verts[2] = new Vector3(0.04f, 0.0f, 0.0f);   // Base right

        Vector3[] normals = new Vector3[3];
        normals[0] = new Vector3(0, 0, 1);
        normals[1] = new Vector3(0, 0, 1);
        normals[2] = new Vector3(0, 0, 1);

        int[] indices = new int[3] { 0, 1, 2 };

        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return arrMesh;
    }
}
