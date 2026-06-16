// NOTE: Shader automatically converted from Godot Engine 4.5.1.stable.mono's StandardMaterial3D.

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform vec4 albedo : source_color;
uniform sampler2D texture_albedo : source_color, filter_linear_mipmap, repeat_enable;
uniform float alpha_scissor_threshold : hint_range(0.0, 1.0, 0.001);
uniform ivec2 albedo_texture_size;
uniform float point_size : hint_range(0.1, 128.0, 0.1);

uniform float roughness : hint_range(0.0, 1.0);
uniform sampler2D texture_metallic : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform vec4 metallic_texture_channel;
uniform sampler2D texture_roughness : hint_roughness_r, filter_linear_mipmap, repeat_enable;

uniform float specular : hint_range(0.0, 1.0, 0.01);
uniform float metallic : hint_range(0.0, 1.0, 0.01);

uniform vec3 uv1_scale;
uniform vec3 uv1_offset;
uniform vec3 uv2_scale;
uniform vec3 uv2_offset;

// --- added wind controls ---
uniform float sway_strength : hint_range(0.0, 1.0) = 0.15;
uniform float sway_speed : hint_range(0.0, 10.0) = 1.5;
uniform float sway_scale : hint_range(0.01, 10.0) = 1.0;
uniform vec3 wind_dir = vec3(1.0, 0.0, 0.3);
// 0 = whole mesh moves, >0 keeps base still and tips move more (assumes grass up is +Y)
uniform float height_mask : hint_range(0.0, 5.0) = 1.0;

void vertex() {
	// keep original UV transform
	UV = UV * uv1_scale.xy + uv1_offset.xy;

	// sway (world-space so different instances don't sync)
	vec3 dir = normalize(vec3(wind_dir.x, 0.0, wind_dir.z));
	float t = TIME * sway_speed;

	vec3 world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;

	float wave =
		sin(world_pos.x * sway_scale + t) * 0.6 +
		sin(world_pos.z * sway_scale * 1.3 + t * 1.2) * 0.4;

	float mask = (height_mask <= 0.0) ? 1.0 : clamp(VERTEX.y * height_mask, 0.0, 1.0);

	VERTEX.xyz += dir * (wave * sway_strength * mask);
}

void fragment() {
	vec2 base_uv = UV;

	vec4 albedo_tex = texture(texture_albedo, base_uv);
	ALBEDO = albedo.rgb * albedo_tex.rgb;

	float metallic_tex = dot(texture(texture_metallic, base_uv), metallic_texture_channel);
	METALLIC = metallic_tex * metallic;

	vec4 roughness_texture_channel = vec4(1.0, 0.0, 0.0, 0.0);
	float roughness_tex = dot(texture(texture_roughness, base_uv), roughness_texture_channel);

	// original values (ignored by the overrides below)
	// ROUGHNESS = roughness_tex * roughness;
	// SPECULAR = specular;

	ALPHA *= albedo.a * albedo_tex.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_scissor_threshold;

	// 🌿 make it fully matte
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}