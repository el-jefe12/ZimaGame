shader_type spatial;

render_mode blend_mix, depth_prepass_alpha, cull_disabled, unshaded;

uniform sampler2D fire_texture : source_color, filter_linear, repeat_disable;

// Your actual texture is 8 cells wide and 8 cells tall.
uniform int hframes = 8;
uniform int sheet_vframes = 8;

// Your sheet layout:
// first row has 8 frames.
// all following used rows have 7 frames.
uniform int first_row_frames = 8;
uniform int other_row_frames = 7;
uniform int frame_count = 50;

uniform float fps = 16.0;
uniform float alpha_cutoff = 0.05;
uniform float emission_strength = 2.0;

// Scale/position the fire inside the plane.
// 1.0 = normal size.
// 0.5 = smaller.
// 2.0 = larger.
uniform vec2 flame_scale = vec2(1.0, 1.0);

// Moves the fire inside the plane after scaling.
uniform vec2 flame_offset = vec2(0.0, 0.0);

// Rotates the UVs inside the plane.
// Use 0, 90, -90, or 180 if the texture is sideways/upside-down.
uniform float uv_rotation_degrees = 0.0;

// If the animation appears upside down, toggle this.
uniform bool texture_origin_top = true;

// Use only if you are seeing the back side of the plane mirrored.
uniform bool fix_backface_mirror = false;

// Simple texture flips.
uniform bool flip_x = false;
uniform bool flip_y = false;

// Keep false if your PNG already has proper transparency.
uniform bool alpha_from_black = false;
uniform float black_alpha_power = 1.0;

vec2 rotate_uv_around_center(vec2 input_uv, float degrees)
{
	float radians_value = radians(degrees);
	float s = sin(radians_value);
	float c = cos(radians_value);

	vec2 centered_uv = input_uv - vec2(0.5);

	vec2 rotated_uv = vec2(
		centered_uv.x * c - centered_uv.y * s,
		centered_uv.x * s + centered_uv.y * c
	);

	return rotated_uv + vec2(0.5);
}

void fragment()
{
	int current_frame = int(floor(TIME * fps)) % frame_count;

	int frame_x = 0;
	int row_from_top = 0;

	// Frame 0-7: first row, all 8 columns used.
	if (current_frame < first_row_frames)
	{
		frame_x = current_frame;
		row_from_top = 0;
	}
	else
	{
		// Frame 8-49:
		// rows after the first have only 7 valid columns.
		int shifted_frame = current_frame - first_row_frames;

		frame_x = shifted_frame % other_row_frames;
		row_from_top = 1 + (shifted_frame / other_row_frames);
	}

	int frame_y = row_from_top;

	if (!texture_origin_top)
	{
		frame_y = (sheet_vframes - 1) - row_from_top;
	}

	vec2 local_uv = UV;

	// First fix backface mirroring, if needed.
	if (fix_backface_mirror && !FRONT_FACING)
	{
		local_uv.x = 1.0 - local_uv.x;
	}

	// Then apply manual flips.
	if (flip_x)
	{
		local_uv.x = 1.0 - local_uv.x;
	}

	if (flip_y)
	{
		local_uv.y = 1.0 - local_uv.y;
	}

	// Rotate the texture inside the plane.
	local_uv = rotate_uv_around_center(local_uv, uv_rotation_degrees);

	// Scale around center.
	// Values below 1.0 make the fire smaller on the plane.
	// Values above 1.0 make it larger.
	local_uv = (local_uv - vec2(0.5)) / flame_scale + vec2(0.5) - flame_offset;

	// Anything outside the scaled fire area becomes transparent.
	if (local_uv.x < 0.0 || local_uv.x > 1.0 || local_uv.y < 0.0 || local_uv.y > 1.0)
	{
		discard;
	}

	vec2 frame_size = vec2(1.0 / float(hframes), 1.0 / float(sheet_vframes));

	vec2 atlas_uv = local_uv * frame_size;
	atlas_uv.x += float(frame_x) * frame_size.x;
	atlas_uv.y += float(frame_y) * frame_size.y;

	vec4 texture_color = texture(fire_texture, atlas_uv);

	float alpha_value = texture_color.a;

	if (alpha_from_black)
	{
		float brightness = max(texture_color.r, max(texture_color.g, texture_color.b));
		alpha_value = pow(brightness, black_alpha_power);
	}

	if (alpha_value < alpha_cutoff)
	{
		discard;
	}

	ALBEDO = texture_color.rgb;
	ALPHA = alpha_value;
	EMISSION = texture_color.rgb * emission_strength;
}