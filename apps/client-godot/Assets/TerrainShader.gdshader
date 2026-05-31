shader_type spatial;

// The 6-channel soil tensor encoded via vertex color channels
// R = Water (0 = dry, 1 = flooded)
// G = Sediment (0 = rock, 1 = fine dirt)
// B = Cohesion (0 = loose sand, 1 = hard clay)
// A = Oxidation (0 = fresh, 1 = rusted/weathered)

uniform vec3 dry_color : source_color = vec3(0.6, 0.5, 0.4);
uniform vec3 mud_color : source_color = vec3(0.25, 0.18, 0.12);
uniform vec3 rock_color : source_color = vec3(0.4, 0.4, 0.45);

void fragment() {
    float wetness = COLOR.r;
    float sediment = COLOR.g;
    
    // Base material is a mix between rock and dirt based on sediment
    vec3 base_color = mix(rock_color, dry_color, sediment);
    
    // Apply wetness (mud)
    vec3 final_color = mix(base_color, mud_color, wetness);
    
    ALBEDO = final_color;
    
    // Mud is glossy and smooth, dry dirt is rough
    ROUGHNESS = mix(0.9, 0.15, wetness);
    
    // Specular highlight increases when wet
    SPECULAR = mix(0.1, 0.8, wetness);
}
