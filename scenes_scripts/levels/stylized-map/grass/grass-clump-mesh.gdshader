shader_type spatial;
render_mode cull_disabled;

// Notice all the parabola, pinch, and skew uniforms are gone!
uniform vec3 color : source_color = vec3(1.0, 0.0, 0.0);
uniform vec3 color_bottom : source_color;
uniform float gradient_strength : hint_range(0.0, 2.0) = 1.0; 

uniform sampler2D noise;
uniform float noise_scale = 20.0;
uniform float mesh_height = 1.0;

varying vec3 world_pos; 
varying float local_height_mask;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    
    // We removed the X-axis displacement math.
    // We are now trusting Blender's physical shape!
    
    // We keep the local height mask so overlapping blades 
    // in your clump blend perfectly together without visual glitches.
    local_height_mask = 1.0 - clamp(VERTEX.y / mesh_height, 0.0, 1.0);
}

void fragment() {
    float gradient_mix = clamp(local_height_mask * gradient_strength, 0.0, 1.0);
    vec3 spatial_base_color = mix(color, color_bottom, gradient_mix);
    
    vec3 noise_level = texture(noise, world_pos.xz / noise_scale).rgb;
    ALBEDO = spatial_base_color * mix(color, color_bottom, noise_level.r);

    NORMAL = (VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz;
}