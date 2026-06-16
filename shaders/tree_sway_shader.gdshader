shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform vec4 albedo : source_color;
uniform sampler2D texture_albedo : source_color, filter_linear_mipmap, repeat_enable;

uniform float alpha_scissor_threshold : hint_range(0.0, 1.0) = 0.5;

uniform vec3 uv1_scale = vec3(1.0);
uniform vec3 uv1_offset = vec3(0.0);

uniform float roughness : hint_range(0.0,1.0) = 1.0;
uniform float specular : hint_range(0.0,1.0) = 0.0;

/* WIND SETTINGS */
uniform float sway_strength : hint_range(0.0,1.0) = 0.15;
uniform float sway_speed : hint_range(0.0,10.0) = 1.5;
uniform float sway_scale : hint_range(0.01,10.0) = 1.0;
uniform vec3 wind_dir = vec3(1.0,0.0,0.3);

/* how much tips move more than base */
uniform float height_mask : hint_range(0.0,5.0) = 0.8;

void vertex() {

	UV = UV * uv1_scale.xy + uv1_offset.xy;

	vec3 dir = normalize(vec3(wind_dir.x,0.0,wind_dir.z));
	float t = TIME * sway_speed;

	vec3 world_pos = (MODEL_MATRIX * vec4(VERTEX,1.0)).xyz;

	float wave =
		sin(world_pos.x * sway_scale + t) * 0.6 +
		sin(world_pos.z * sway_scale * 1.3 + t * 1.2) * 0.4;

	float mask = clamp(VERTEX.y * height_mask,0.0,1.0);

	VERTEX.xyz += dir * wave * sway_strength * mask;
}

void fragment() {

	vec4 tex = texture(texture_albedo,UV);

	ALBEDO = albedo.rgb * tex.rgb;
	ALPHA = tex.a;

	ALPHA_SCISSOR_THRESHOLD = alpha_scissor_threshold;

	ROUGHNESS = roughness;
	SPECULAR = specular;
}