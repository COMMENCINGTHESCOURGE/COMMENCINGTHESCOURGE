shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

// Phase 27: The Tensor Vegetation Shader
// Ported directly from Filament GLSL baseline

uniform vec3 windDirection = vec3(1.0, 0.0, 0.3);
uniform float windStrength = 1.2;
uniform float noiseScale = 0.15;
uniform vec3 cameraPosition = vec3(0.0, 2.0, 15.0);
uniform float lodDistance = 60.0;

uniform vec4 baseColor : source_color = vec4(0.3, 0.5, 0.2, 1.0);
uniform float roughnessValue = 0.8;
uniform float metallicValue = 0.0;

// Phase 28: Material Tensor SSBO fallback (1D texture)
uniform sampler2D materialTensor;
const float MATERIAL_COUNT = 256.0;

float hash3D(vec3 p) {
    return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

float noise3D(vec3 p) {
    vec3 i = floor(p);
    vec3 f = fract(p);
    vec3 u = f * f * (3.0 - 2.0 * f);

    float c000 = hash3D(i);
    float c100 = hash3D(i + vec3(1.0, 0.0, 0.0));
    float c010 = hash3D(i + vec3(0.0, 1.0, 0.0));
    float c110 = hash3D(i + vec3(1.0, 1.0, 0.0));
    float c001 = hash3D(i + vec3(0.0, 0.0, 1.0));
    float c101 = hash3D(i + vec3(1.0, 0.0, 1.0));
    float c011 = hash3D(i + vec3(0.0, 1.0, 1.0));
    float c111 = hash3D(i + vec3(1.0, 1.0, 1.0));

    return mix(
        mix(mix(c000, c100, u.x),
            mix(c010, c110, u.x), u.y),
        mix(mix(c001, c101, u.x),
            mix(c011, c111, u.x), u.y),
        u.z
    );
}

float fbm(vec3 p) {
    float value = 0.0;
    float amp = 0.6;
    float freq = 1.0;
    value += amp * noise3D(p * freq);
    freq *= 2.0; amp *= 0.5;
    value += amp * noise3D(p * freq);
    return value;
}

void vertex() {
    // Per‑instance world position from INSTANCE_CUSTOM attribute
    vec3 worldPos = INSTANCE_CUSTOM.xyz;
    
    // Phase 28: Material Index from CUSTOM1 equivalent (INSTANCE_CUSTOM.w)
    float matIdx = INSTANCE_CUSTOM.w;
    vec2 tensorUV = vec2((matIdx + 0.5) / MATERIAL_COUNT, 0.5);
    vec4 matData = texture(materialTensor, tensorUV);
    float cohesion = matData.r; // 0.0 (rigid) to 1.0 (flexible)

    float dist = distance(worldPos, cameraPosition);
    float fade = 1.0 - smoothstep(lodDistance * 0.7, lodDistance, dist);

    // Grass‑specific parameters
    float flutterSpeed = 8.5;
    float response      = 0.35;
    float swayAmount    = 0.12;

    // Phase 28: Wind animation modulated by cohesion
    float windFactor = mix(0.2, 1.0, cohesion);

    float t = TIME * flutterSpeed;
    vec3 windPos = worldPos * noiseScale + vec3(t * 0.3, t * 0.5, t * 0.4);
    float windVal = fbm(windPos);
    float sway = sin(TIME * 1.2 + worldPos.x * 0.3) * swayAmount * windFactor;
    float force = windStrength * response * windVal * fade * windFactor;

    float rotZ = windDirection.x * force;
    float rotX = windDirection.z * force;
    
    mat3 rot = mat3(
        vec3(cos(rotZ), -sin(rotZ), 0.0),
        vec3(sin(rotZ),  cos(rotZ), 0.0),
        vec3(0.0,        0.0,       1.0)
    ) * mat3(
        vec3(1.0, 0.0,       0.0),
        vec3(0.0, cos(rotX), -sin(rotX)),
        vec3(0.0, sin(rotX),  cos(rotX))
    );

    VERTEX = rot * VERTEX;
    VERTEX.x += sway;
}

void fragment() {
    ALBEDO = baseColor.rgb;
    ROUGHNESS = roughnessValue;
    METALLIC = metallicValue;
}
