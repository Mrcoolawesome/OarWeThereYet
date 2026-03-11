shader_type spatial;
render_mode cull_disabled;

// Notice all the parabola, pinch, and skew uniforms are gone!
// For an RGBA color with transparency (Defaults to semi-transparent red)
uniform vec3 color : source_color = vec3(1.0, 0.0, 0.0);
uniform vec3 color_bottom : source_color;
// It controls how fast it transitions to the bottom color.
uniform float gradient_strength : hint_range(0.0, 2.0) = 1.0;

uniform sampler2D noise;
uniform float noise_scale = 20.0;

// Set this to the approximate max height of your grass mesh in Godot.
// If your mesh is 1 unit tall, leave it at 1.0.
uniform float mesh_height = 1.0;

// --- NEW WIND UNIFORMS ---
// The direction the wind blows
uniform vec2 wind_direction = vec2(1.0, 0.5);
// How fast the noise pans
uniform float wind_speed = 0.03;
// How far the grass bends
uniform float wind_strength = 1.0;

// used for the noise texture
varying vec3 world_pos;
// This passes the calculated height from the vertex to the fragment shader.
varying float local_height_mask;

// --- NEW VARYING ---
// This passes the moving wind coordinates to the fragment shader so the colors sync!
varying vec2 wind_uv;

void vertex() {
    // get the world position of the current vertex
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;

    // We removed the X-axis displacement math.
    // We are now trusting Blender's physical shape!

    // We keep the local height mask so overlapping blades
    // in your clump blend perfectly together without visual glitches.
    // We divide by mesh_height to normalize it between 0.0 and 1.0.
    // We invert it (1.0 - ...) so the base is 1.0 and the top is 0.0.
    local_height_mask = 1.0 - clamp(VERTEX.y / mesh_height, 0.0, 1.0);

    // --- NEW WIND LOGIC ---
    // 1. Create a mask so the roots stay still.
    // VERTEX.y is 0 at the bottom. Dividing by mesh_height normalizes it from 0.0 to 1.0.
    float sway_mask = clamp(VERTEX.y / mesh_height, 0.0, 1.0);

    // Squaring the mask makes it bend on a curve instead of a straight line
    sway_mask *= sway_mask;

    // 2. Pan the noise texture over time (Assigned to our new varying variable)
    wind_uv = (world_pos.xz / noise_scale) + (normalize(wind_direction) * TIME * wind_speed);

    // 3. Sample the noise and shift it from [0 to 1] to [-1 to 1] so it sways back and forth
    float wind_sample = (texture(noise, wind_uv).r - 0.5) * 2.0;

    // 4. Apply the displacement to the X and Z axes
    VERTEX.xz += normalize(wind_direction) * wind_sample * wind_strength * sway_mask;
}

void fragment() {
    // 1. Calculate the spatial vertical gradient (replacing UV.y)
    float gradient_mix = clamp(local_height_mask * gradient_strength, 0.0, 1.0);
    vec3 spatial_base_color = mix(color, color_bottom, gradient_mix);

    // 2. Sample the noise texture (Now using the moving wind_uv!)
    vec3 noise_level = texture(noise, wind_uv).rgb;

    // 3. Combine the spatial gradient with the noise variation
    // the color is randomized
    ALBEDO = spatial_base_color * mix(color, color_bottom, noise_level.r);

    // 4. Override the normals to always point straight up at the sky.
    NORMAL = (VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz;
}