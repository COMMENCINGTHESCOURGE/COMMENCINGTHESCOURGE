using Godot;
using System;
using System.Runtime.InteropServices;

/// <summary>
/// Phase 29: GPU Compute Culling
/// Dispatches the grass_culler.glsl compute shader to compact the instance buffer.
/// </summary>
public partial class ComputeCuller : Node
{
    private RenderingDevice _rd;
    private Rid _shader;
    private Rid _pipeline;
    private Rid _uniformSet;

    private Rid _inputBuffer;
    private Rid _outputBuffer;
    private Rid _counterBuffer;

    private int _maxInstances = 10000;
    private bool _initialized = false;
    private float[] _inputData;
    
    // Reference to the active camera
    private Camera3D _camera;

    public void Initialize(float[] rawInstanceData, int maxInstances, Texture2D tensorTexture)
    {
        _maxInstances = maxInstances;
        _inputData = rawInstanceData;
        
        _rd = RenderingServer.CreateLocalRenderingDevice();
        if (_rd == null)
        {
            GD.PrintErr("ComputeCuller: Local RenderingDevice not supported.");
            return;
        }

        // Load shader
        var shaderFile = GD.Load<RDShaderFile>("res://Shaders/grass_culler.glsl");
        if (shaderFile == null)
        {
            GD.PrintErr("ComputeCuller: Failed to load grass_culler.glsl");
            return;
        }
        var spirv = shaderFile.GetSpirV();
        _shader = _rd.ShaderCreateFromSpirV(spirv);
        _pipeline = _rd.ComputePipelineCreate(_shader);

        // Buffers (16 floats per instance = 64 bytes)
        uint bufferSize = (uint)(_maxInstances * 64);
        
        byte[] inputBytes = new byte[bufferSize];
        Buffer.BlockCopy(_inputData, 0, inputBytes, 0, inputBytes.Length);
        _inputBuffer = _rd.StorageBufferCreate(bufferSize, inputBytes);
        
        _outputBuffer = _rd.StorageBufferCreate(bufferSize);
        
        uint[] counterInit = new uint[] { 0 };
        byte[] counterBytes = new byte[4];
        Buffer.BlockCopy(counterInit, 0, counterBytes, 0, 4);
        _counterBuffer = _rd.StorageBufferCreate(4, counterBytes);

        // Uniforms
        var inputUniform = new RDUniform();
        inputUniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
        inputUniform.Binding = 0;
        inputUniform.AddId(_inputBuffer);

        var outputUniform = new RDUniform();
        outputUniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
        outputUniform.Binding = 1;
        outputUniform.AddId(_outputBuffer);

        var counterUniform = new RDUniform();
        counterUniform.UniformType = RenderingDevice.UniformType.StorageBuffer;
        counterUniform.Binding = 2;
        counterUniform.AddId(_counterBuffer);

        // Binding 3 is the material tensor texture. 
        // We need to pass the RID of the ImageTexture to the RenderingDevice.
        var tensorUniform = new RDUniform();
        tensorUniform.UniformType = RenderingDevice.UniformType.Sampler;
        tensorUniform.Binding = 3;
        // In Godot 4, binding a texture to compute requires TextureGetRdTexture
        Rid rdTexture = RenderingServer.TextureGetRdTexture(tensorTexture.GetRid());
        if (rdTexture.IsValid)
        {
            tensorUniform.UniformType = RenderingDevice.UniformType.Image;
            tensorUniform.AddId(rdTexture);
        }

        var uniforms = new Godot.Collections.Array<RDUniform>();
        uniforms.Add(inputUniform);
        uniforms.Add(outputUniform);
        uniforms.Add(counterUniform);
        
        if (rdTexture.IsValid)
            uniforms.Add(tensorUniform);

        _uniformSet = _rd.UniformSetCreate(uniforms, _shader, 0);
        
        _camera = GetViewport().GetCamera3D();
        _initialized = true;
    }

    public float[] DispatchCull(float lodDistance)
    {
        if (!_initialized || _camera == null) return null;

        // Reset counter
        uint[] counterReset = new uint[] { 0 };
        byte[] counterBytes = new byte[4];
        Buffer.BlockCopy(counterReset, 0, counterBytes, 0, 4);
        _rd.BufferUpdate(_counterBuffer, 0, 4, counterBytes);

        // Prepare push constants
        Vector3 camPos = _camera.GlobalPosition;
        float[] pushConstants = new float[] { camPos.X, camPos.Y, camPos.Z, lodDistance };
        byte[] pushBytes = new byte[16];
        Buffer.BlockCopy(pushConstants, 0, pushBytes, 0, 16);

        // Compute List
        long computeList = _rd.ComputeListBegin();
        _rd.ComputeListBindComputePipeline(computeList, _pipeline);
        _rd.ComputeListBindUniformSet(computeList, _uniformSet, 0);
        _rd.ComputeListSetPushConstant(computeList, pushBytes, (uint)pushBytes.Length);
        
        // Dispatch (64 workgroups)
        _rd.ComputeListDispatch(computeList, (uint)Mathf.CeilToInt(_maxInstances / 64.0f), 1, 1);
        _rd.ComputeListEnd();

        // Submit and wait
        _rd.Submit();
        _rd.Sync();

        // Read counter
        byte[] countOutputBytes = _rd.BufferGetData(_counterBuffer);
        uint visibleCount = BitConverter.ToUInt32(countOutputBytes, 0);

        if (visibleCount == 0) return new float[0];

        // Read output instances
        byte[] outDataBytes = _rd.BufferGetData(_outputBuffer, 0, visibleCount * 64);
        float[] compactedData = new float[visibleCount * 16];
        Buffer.BlockCopy(outDataBytes, 0, compactedData, 0, outDataBytes.Length);

        return compactedData;
    }

    public override void _ExitTree()
    {
        if (_initialized && _rd != null)
        {
            _rd.FreeRid(_inputBuffer);
            _rd.FreeRid(_outputBuffer);
            _rd.FreeRid(_counterBuffer);
            _rd.FreeRid(_uniformSet);
            _rd.FreeRid(_pipeline);
            _rd.FreeRid(_shader);
            _rd.Free();
            _rd = null;
        }
    }
}
