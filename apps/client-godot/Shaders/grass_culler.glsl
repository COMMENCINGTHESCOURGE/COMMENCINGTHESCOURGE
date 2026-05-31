#[compute]
#version 450

// Phase 29: GPU Compute Culling
// Compacts a raw array of instances based on distance and yield.

layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;

struct InstanceData {
    vec4 transform_0;
    vec4 transform_1;
    vec4 transform_2;
    vec4 custom_data; // x,y,z = original pos, w = material index
};

// Input buffer containing all 10,000 instances
layout(set = 0, binding = 0, std430) restrict readonly buffer InputBuffer {
    InstanceData input_instances[];
};

// Output buffer containing only the visible/active instances
layout(set = 0, binding = 1, std430) restrict writeonly buffer OutputBuffer {
    InstanceData output_instances[];
};

// Counter buffer for indirect draw or CPU readback of final count
layout(set = 0, binding = 2, std430) restrict buffer CounterBuffer {
    uint count;
};

// Uniforms (push constants for fast access)
layout(push_constant, std430) uniform Params {
    vec4 camera_pos; // xyz = pos, w = max distance
} params;

// A simple 1D texture mimicking the material tensor (since we can't easily bind the exact same sampler2D in Godot compute without extra sets, we pass a stripped version or just rely on distance for now).
layout(set = 0, binding = 3) uniform sampler2D materialTensor;

void main() {
    uint idx = gl_GlobalInvocationID.x;
    if (idx >= input_instances.length()) {
        return;
    }

    InstanceData instance = input_instances[idx];
    
    // Extract world position from the transform (usually transform_0.w, transform_1.w, transform_2.w if transposed, but in Godot Transform3D is stored col-major. Let's rely on custom_data.xyz which stores original spawn pos)
    vec3 worldPos = instance.custom_data.xyz;
    
    float dist = distance(worldPos, params.camera_pos.xyz);
    
    // Cull by distance
    if (dist > params.camera_pos.w) {
        return; // Culled!
    }

    // Material Yield Culling
    float matIdx = instance.custom_data.w;
    vec2 tensorUV = vec2((matIdx + 0.5) / 256.0, 0.5);
    vec4 matData = texture(materialTensor, tensorUV);
    float yield = matData.g; // yield is green channel
    
    if (yield <= 0.05) {
        return; // Depleted! Culled!
    }

    // Passed all tests, atomicaly increment counter and write to output buffer
    uint out_idx = atomicAdd(count, 1);
    output_instances[out_idx] = instance;
}
