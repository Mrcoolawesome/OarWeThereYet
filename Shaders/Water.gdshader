shader_type spatial;

uniform vec3 albedo : source_color; // variable we can edit in the script
uniform vec3 albedo2 : source_color;
uniform float metallic : hint_range(0.0, 1.0) = 0; // sets a range of possible values and also a default value
uniform float roughness : hint_range(0.0, 1.0) = 0.02;
uniform sampler2D wave;
uniform sampler2D texture_normal;
uniform sampler2D texture_normal2;
uniform vec2 wave_direction = vec2(2.0, 0.0);
uniform vec2 wave_direction2 = vec2(0.0, 1.0);
uniform float wave_speed : hint_range(0.0, 0.2, 0.005) = 0.025;
uniform float noise_scale = 10.0;
uniform float height_scale = 0.15;
uniform float wave_time = 1.0;

// edge foam
uniform float edge_scale = 0.1;
uniform float near = 1.0;
uniform float far = 100.0;
uniform vec3 edge_color : source_color;

// for depth stuff
uniform vec4 color_deep : source_color;
uniform vec4 color_shallow : source_color;
uniform float beers_law = 2.0;
uniform float depth_offset = -0.75;
uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_linear_mipmap;
uniform sampler2D SCREEN_TEXTURE: hint_screen_texture, filter_linear_mipmap;

// varying variables
varying float height;
varying vec3 world_pos;

// this is for actual displacement
void vertex() {
	// Called for every vertex the material is visible on.
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; // set the world position for each vertex
	// take the noise texture and take the r value (ranges from 0-1) at the location of each vertex per the texture
	height = texture(wave, world_pos.xz / noise_scale + wave_time * wave_speed).r; // we also use time to animate it
	VERTEX.y += height * height_scale; // the height is added to the height scale and then set to the verticies new height
}

float fresnel(float amount, vec3 normal, vec3 view) {
	return pow((1.0 - clamp(dot(normalize(normal), normalize(view)), 0.0, 1.0)), amount);
}

// calculate edge foam
float edge(float depth) {
	depth = 1.0 - 2.0 * depth;
	return near * far / (far + depth * (near - far));
}

void fragment() {
	// Called for every pixel the material is visible on.
	// depth variables and calculation
	// it grabs the depth texture from the viewport, then applies a blend and coloration of the depth gradient
	//float depth_texture = texture(DEPTH_TEXTURE, SCREEN_UV).r * 2.0 - 1.0;
	//float depth = PROJECTION_MATRIX[3][2] / (depth_texture + PROJECTION_MATRIX[2][2]);
	//float depth_blend = exp((depth + VERTEX.z + depth_offset) * -beers_law);
	//depth_blend = clamp(1.0 - depth_blend, 0.0, 1.0);
	//float depth_blend_power = clamp(pow(depth_blend, 2.5), 0.0, 1.0);

	// This is the 'new and correct' way of doing it according to soemone in the comments
	// It does in fact work better than the original code
	float depth_texture = texture(DEPTH_TEXTURE, SCREEN_UV).x;
	vec3 ndc = vec3(SCREEN_UV * 2.0 -1.0, depth_texture);
	vec4 view = INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
	view.xyz /= view.w;
	float depth_blend = exp((-view.z + VERTEX.z + depth_offset) * -beers_law);
	float depth_blend_power = clamp(pow(depth_blend, 2.5), 0.0, 1.0);

	vec3 screen_color = textureLod(SCREEN_TEXTURE, SCREEN_UV, depth_blend_power * 2.5).rgb;
	vec3 depth_color = mix(color_deep.rgb, color_shallow.rgb, depth_blend_power);
	vec3 color = mix(screen_color * depth_color, depth_color * 0.25, depth_blend_power * 0.5);

	// getting edge depth calculations
	float z_depth = edge(texture(DEPTH_TEXTURE, SCREEN_UV).x);
	float z_pos = edge(FRAGCOORD.z);
	float z_dif = z_depth - z_pos;

	// time calculations for wave normal map movement
	vec2 time = (TIME * wave_direction) * wave_speed; // movement of first wave
	vec2 time2 = (TIME * wave_direction2) * wave_speed; // movment rate of second wave
	// setting the texture
	vec3 normal_blend = mix(texture(texture_normal, world_pos.xz + time).rgb, texture(texture_normal2,world_pos.xz + time2).rgb, 0.5);

	// calculate the fresnel stuff
	float fresnel = fresnel(5.0, NORMAL, VIEW);
	vec3 surface_color = mix(albedo, albedo2, fresnel); // interpolate albedo values using fresnel

	vec3 depth_color_with_edge = mix(edge_color, color, step(edge_scale, z_dif));

	// set the albedo
	ALBEDO = clamp(surface_color + depth_color_with_edge, vec3(0.0), vec3(1.0));
	METALLIC = metallic;
	ROUGHNESS = roughness;
	NORMAL_MAP = normal_blend;
}

//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}
