shader_type canvas_item;

uniform vec4 blush_tint : source_color = vec4(1.0, 0.75, 0.75, 1.0);
uniform float blush_strength : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	COLOR = mix(tex, tex * blush_tint, blush_strength);
}
