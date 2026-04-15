shader_type spatial;
// cull_front is the magic word here. It only draws the INSIDE of the mesh.
render_mode unshaded, cull_front, depth_draw_always;

uniform vec4 outline_color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float outline_width = 0.05; // Adjust thickness here

void vertex() {
    // Pushes the vertices outward slightly along their normals to make the mesh "fatter"
    VERTEX += NORMAL * outline_width;
}

void fragment() {
    ALBEDO = outline_color.rgb;
    ALPHA = outline_color.a;
}