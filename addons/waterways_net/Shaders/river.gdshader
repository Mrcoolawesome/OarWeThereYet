// Copyright © 2022 Kasper Arnklit Frandsen - MIT License
// See `LICENSE.md` included in the source distribution for details.
shader_type spatial;
render_mode depth_draw_always, specular_schlick_ggx, cull_disabled;

uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_linear_mipmap;
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

// --- NEW CUSTOM BUMPY PARAMETERS ---
uniform sampler2D custom_noise_map : hint_default_black;
uniform float custom_wave_height : hint_range(0.0, 2.0) = 0.2;
uniform float custom_noise_scale : hint_range(0.1, 50.0) = 10.0;

// Main
uniform float normal_scale : hint_range(-16.0, 16.0) = 1.0;
uniform sampler2D normal_bump_texture : hint_normal;
uniform vec3 uv_scale = vec3(1.0, 1.0, 1.0);
uniform float roughness : hint_range(0.0, 1.0) = 0.2;
uniform float edge_fade : hint_range(0.0, 1.0) = 0.25;

// Albedo
uniform mat4 albedo_color = mat4(vec4(0.0, 0.15, 0.0, 0.0), vec4(0.8, 0.2, 0.0, 0.0), vec4(1.0, 0.5, 0.0, 0.0), vec4(0.0));
uniform float albedo_depth : hint_range(0.0, 200.0) = 10.0;
uniform float albedo_depth_curve = 0.25;

// Transparency
uniform float transparency_clarity : hint_range(0.0, 200.0) = 10.0;
uniform float transparency_depth_curve = 0.25;
uniform float transparency_refraction : hint_range(-1.0, 1.0) = 0.05;

// Flow
uniform float flow_speed : hint_range(0.0, 10.0) = 1.0;
uniform float flow_base : hint_range(0.0, 8.0) = 0.0;
uniform float flow_steepness : hint_range(0.0, 8.0) = 2.0;
uniform float flow_distance : hint_range(0.0, 8.0) = 1.0;
uniform float flow_pressure : hint_range(0.0, 8.0) = 1.0;
uniform float flow_max : hint_range(0.0, 8.0) = 4.0;

// Foam
uniform vec4 foam_color : source_color = vec4(0.9, 0.9, 0.9, 1.0);
uniform float foam_amount : hint_range(0.0, 4.0) = 2.0;
uniform float foam_steepness : hint_range(0.0, 8.0) = 2.0;
uniform float foam_smoothness : hint_range(0.0, 1.0) = 0.3;

// Internal uniforms - DO NOT CUSTOMIZE THESE
uniform float i_lod0_distance : hint_range(5.0, 200.0) = 50.0;
uniform sampler2D i_texture_foam_noise : hint_default_white;
uniform sampler2D i_flowmap : hint_normal;
uniform sampler2D i_distmap : hint_default_white;
uniform bool i_valid_flowmap = false;
uniform int i_uv2_sides = 2;

// --- NEW VARYING FOR DIRECTIONAL FLOW ---
varying vec2 river_flow_direction;

vec3 FlowUVW(vec2 uv_in, vec2 flowVector, vec2 jump, vec3 tiling, float time, bool flowB) {
    float phaseOffset = flowB ? 0.5 : 0.0;
    float progress = fract(time + phaseOffset);
    vec3 uvw;
    uvw.xy = uv_in - flowVector * (progress - 0.5);
    uvw.xy *= tiling.xy;
    uvw.xy += phaseOffset;
    uvw.xy += (time - progress) * jump;
    uvw.z = 1.0 - abs(1.0 - 2.0 * progress);
    return uvw;
}

float ease(float p_x, float p_c) {
    if (p_x < 0.0) { p_x = 0.0; } else if (p_x > 1.0) { p_x = 1.0; }
    if (p_c > 0.0) {
        if (p_c < 1.0) { return 1.0 - pow(1.0 - p_x, 1.0 / p_c); } else { return pow(p_x, p_c); }
    } else if (p_c < 0.0) {
        if (p_x < 0.5) { return pow(p_x * 2.0, -p_c) * 0.5; } else { return (1.0 - pow(1.0 - (p_x - 0.5) * 2.0, -p_c)) * 0.5 + 0.5; }
    } else { return 0.0; }
}

float lin2srgb(float lin) { return pow(lin, 2.2); }

mat4 gradient_lin2srgb(mat4 lin_mat) {
    mat4 srgb_mat = mat4(
        vec4(lin2srgb(lin_mat[0].x), lin2srgb(lin_mat[0].y), lin2srgb(lin_mat[0].z), lin2srgb(lin_mat[0].w)),
        vec4(lin2srgb(lin_mat[1].x), lin2srgb(lin_mat[1].y), lin2srgb(lin_mat[1].z), lin2srgb(lin_mat[1].w)),
        vec4(0.0), vec4(0.0)
    );
    return srgb_mat;
}

// --- NEW VERTEX FUNCTION FOR BUMPY MESH ---
void vertex() {
    // 1. Get flow data from the internal flowmap
    vec2 custom_UV = (UV2 + 1.0 / float(i_uv2_sides)) * (float(i_uv2_sides) / float(i_uv2_sides + 2));
    vec2 raw_flow = texture(i_flowmap, custom_UV).xy;
    
    // 2. Convert to direction and pass to fragment
    river_flow_direction = (raw_flow - 0.5) * 2.0;
    
    // 3. Move the noise along the river flow
    // We use the mesh UV.y for directional scrolling along the river's path
    vec2 scrolling_uv = vec2(UV.x, UV.y - (TIME * flow_speed * 0.1));
    float noise_height = (texture(custom_noise_map, scrolling_uv * custom_noise_scale).r - 0.5) * 2.0;
    
    // 4. Apply displacement
    VERTEX.y += noise_height * custom_wave_height;
}

void fragment() {
    // --- NEW LOW POLY LIGHTING ---
    // This forces the faceted look by using the geometry's screen derivatives
    NORMAL = normalize(cross(dFdx(VERTEX), dFdy(VERTEX)));

    // Sample the UV2 textures
    vec2 custom_UV = (UV2 + 1.0 / float(i_uv2_sides)) * (float(i_uv2_sides) / float(i_uv2_sides + 2));
    vec4 flow_foam_noise = textureLod(i_flowmap, custom_UV, 0.0);
    vec2 dist_pressure = textureLod(i_distmap, custom_UV, 0.0).xy;
    
    vec2 flow;
    float distance_map;
    float pressure_map;
    float foam_mask;
    if (i_valid_flowmap) {
        flow = flow_foam_noise.xy;
        distance_map = (1.0 - dist_pressure.r) * 2.0;
        pressure_map = dist_pressure.g * 2.0;
        foam_mask = flow_foam_noise.b;
    } else {
        flow = vec2(0.5, 0.572);
        distance_map = 0.5;
        pressure_map = 0.5;
        foam_mask = 0.0;
    }
    
    flow = (flow - 0.5) * 2.0; 
    
    vec3 flow_viewspace = flow.x * TANGENT + flow.y * BINORMAL;
    vec3 up_viewspace = (VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz;
    float steepness_map = max(0.0, dot(flow_viewspace, up_viewspace)) * 4.0;
    
    float flow_force = min(flow_base + steepness_map * flow_steepness + distance_map * flow_distance + pressure_map * flow_pressure, flow_max);
    flow *= flow_force;
    
    vec2 jump1 = vec2(0.24, 0.2083333);
    vec2 jump2 = vec2(0.20, 0.25);
    float time = TIME * flow_speed + flow_foam_noise.a;
    vec3 flow_uvA = FlowUVW(UV, flow, jump1, uv_scale, time, false);
    vec3 flow_uvB = FlowUVW(UV, flow, jump1, uv_scale, time, true);
    vec3 flowx2_uvA = FlowUVW(UV, flow, jump2, uv_scale * 2.0, time, false);
    vec3 flowx2_uvB = FlowUVW(UV, flow, jump2, uv_scale * 2.0, time, true);
    
    vec3 water_a = texture(normal_bump_texture, flow_uvA.xy).rgb;
    vec3 water_b = texture(normal_bump_texture, flow_uvB.xy).rgb;
    vec3 water = water_a * flow_uvA.z + water_b * flow_uvB.z;

    vec2 water_norFBM = water.rg;
    float water_foamFBM = water.b;

    if (VERTEX.z < i_lod0_distance) {
        vec3 waterx2_a = texture(normal_bump_texture, flowx2_uvA.xy).rgb;
        vec3 waterx2_b = texture(normal_bump_texture, flowx2_uvB.xy, 0.0).rgb;
        vec3 waterx2 = waterx2_a * flowx2_uvA.z + waterx2_b * flowx2_uvB.z;

        water_norFBM *= 0.65;
        water_norFBM += waterx2.rg * 0.35;
        water_foamFBM *= waterx2.b * 2.0;
    }
    
    float foam_randomness = texture(i_texture_foam_noise, UV * uv_scale.xy).r;
    
    foam_mask += steepness_map * foam_steepness * foam_randomness;
    foam_mask = clamp(foam_mask, 0.0, 1.0);
    water_foamFBM = clamp((water_foamFBM * foam_amount) - (0.5 / foam_amount), 0.0, 1.0);
    float foam_smooth = clamp(water_foamFBM * foam_mask, 0.0, 1.0);
    float foam_sharp = clamp(water_foamFBM - (1.0 - foam_mask), 0.0, 1.0);
    float combined_foam = mix(foam_sharp, foam_smooth, foam_smoothness);

    float depth_tex = textureLod(DEPTH_TEXTURE, SCREEN_UV, 0.0).r;
    vec4 world_pos_calc = INV_PROJECTION_MATRIX * vec4(SCREEN_UV*2.0-1.0,depth_tex,1.0);
    world_pos_calc.xyz/=world_pos_calc.w;
    float water_depth = VERTEX.z - world_pos_calc.z;
        
    float alb_t = clamp(water_depth / albedo_depth, 0.0, 1.0);
    alb_t = ease(alb_t, albedo_depth_curve);
    SPECULAR = 0.25;
    ROUGHNESS = roughness;
    
    // Disable normal map for true flat look, or keep it for micro-ripples
    // NORMAL_MAP = vec3(water_norFBM, 0);
    // NORMAL_MAP_DEPTH = normal_scale;

    vec3 unpacted_normals = vec3(water_norFBM, 0) * 2.0 - 1.0;
    vec3 ref_normal = normalize(TANGENT * unpacted_normals.x + BINORMAL * unpacted_normals.y) * normal_scale * .1;
    float clar_t = clamp(water_depth / transparency_clarity, 0.0, 1.0);
    vec2 ref_ofs = SCREEN_UV - ref_normal.xy * transparency_refraction * clar_t;
    clar_t = ease(clar_t, transparency_depth_curve);
    float ref_amount = 1.0 - clamp(clar_t + combined_foam, 0.0, 1.0);

    float refracted_depth_tex = textureLod(DEPTH_TEXTURE, ref_ofs, 0.0).r;
    vec4 refracted_world_pos = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, refracted_depth_tex, 1.0);
    refracted_world_pos.xyz /= refracted_world_pos.w;
    float refracted_water_depth = VERTEX.z - refracted_world_pos.z;
    
    if (refracted_world_pos.z > VERTEX.z) {
        ref_ofs = SCREEN_UV;
    } else {
        clar_t = clamp(refracted_water_depth / transparency_clarity, 0.0, 1.0);
        clar_t = ease(clar_t, transparency_depth_curve);
        ref_amount = 1.0 - clamp(clar_t + combined_foam, 0.0, 1.0);
        alb_t = clamp(refracted_water_depth / albedo_depth, 0.0, 1.0);
        alb_t = ease(alb_t, albedo_depth_curve);
    }

    mat4 albedo_color_srgb = gradient_lin2srgb(albedo_color);
    vec3 albedo_color_near = vec3(albedo_color_srgb[0].x, albedo_color_srgb[0].y, albedo_color_srgb[0].z);
    vec3 albedo_color_far = vec3(albedo_color_srgb[1].x, albedo_color_srgb[1].y, albedo_color_srgb[1].z);
    vec3 alb_mix = mix(albedo_color_near.rgb, albedo_color_far.rgb, alb_t);
    ALBEDO = mix(alb_mix, foam_color.rgb, combined_foam);
    ALBEDO *= 1.0 - ref_amount;
    EMISSION += textureLod(SCREEN_TEXTURE, ref_ofs, ROUGHNESS * water_depth).rgb * ref_amount;
    ALPHA = 1.0;
    ALPHA *= clamp(1.0 - smoothstep(world_pos_calc.z + edge_fade, world_pos_calc.z, VERTEX.z), 0.0, 1.0);
}