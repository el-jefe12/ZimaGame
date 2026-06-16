shader_type spatial;

uniform float TimeScale = 0.10;

uniform sampler2D Depth : hint_depth_texture, repeat_disable, filter_nearest;
uniform sampler3D Noise : repeat_enable;

uniform vec3 Color : source_color = vec3(1.0, 0.5, 0.0);

// Shoreline border / foam
uniform float BorderWidth = 0.25;
uniform float BorderSoftness = 0.10;
uniform float BorderNoise = 0.15;
uniform vec3 BorderColor : source_color = vec3(1.0);
uniform float BorderIntensity = 1.0;

// Waves (vertex displacement)
uniform float WaveAmplitude = 0.35;
uniform float WaveSpeed = 0.45;
uniform float WaveScale = 0.06;

// rocking back/forth direction
uniform vec2  WaveDirA = vec2(1.0, 0.2);
uniform vec2  WaveDirB = vec2(-0.3, 1.0);
uniform float RockSpeed = 0.18;
uniform float RockAmount = 1.0;

// Ripples (fragment normal detail)
uniform float RippleNormalStrength = 1.0;
uniform float RippleTiling = 24.0;
uniform float RippleFlowSpeed = 0.15;
uniform float RippleBlend = 0.65;

// Depth-based shore calm
uniform float ShoreCalmStart = 0.05;
uniform float ShoreCalmEnd = 0.60;
uniform float ShoreCalmPower = 1.5;

// Wave prominence controls
uniform float WaveProminence = 1.0;
uniform float VertexWaveProminence = 1.0;
uniform float RippleProminence = 1.0;
uniform float FoamProminence = 1.0;

// Reflection-shimmer killers
uniform float SpecularLevel = 0.12;
uniform float RoughnessLevel = 0.22;
uniform float MaxRippleMix = 0.25;

varying flat mat4 inv_proj;
varying vec2 v_uv;
varying vec2 v_xz;

// helpers
float noise01(vec3 p) { return texture(Noise, p).r; }
float noise11(vec3 p) { return noise01(p) * 2.0 - 1.0; }

float fbm11(vec3 p) {
	float n1 = noise11(p);
	float n2 = noise11(p * 2.0 + vec3(17.2, 0.0, 5.4));
	return (n1 * 0.65 + n2 * 0.35);
}

vec2 rocking_dir(float t) {
	float s = 0.5 + 0.5 * sin(t * RockSpeed * 6.2831853);
	vec2 a = normalize(WaveDirA);
	vec2 b = normalize(WaveDirB);
	return normalize(mix(a, b, s * RockAmount));
}

float linearize_depth(float depth01, vec2 screen_uv) {
	vec3 ndc = vec3(screen_uv * 2.0 - 1.0, depth01);
	vec4 view = inv_proj * vec4(ndc, 1.0);
	view.xyz /= view.w;
	return -view.z;
}

float ripple_height(vec2 p, float t) {
	float h1 = fbm11(vec3(p, t * 0.15));
	float h2 = fbm11(vec3(p * 2.0 + vec2(3.7, 1.9), t * 0.22));
	return h1 * 0.7 + h2 * 0.3;
}

float saturate(float x) { return clamp(x, 0.0, 1.0); }

// Vertex
void vertex() {
	inv_proj = INV_PROJECTION_MATRIX;

	float t = TIME * TimeScale;

	v_uv = UV;
	v_xz = VERTEX.xz;

	vec3 final_normal = vec3(0.0, 1.0, 0.0);

	{
		vec2 dir = rocking_dir(t);
		vec2 flow = dir * (t * WaveSpeed);

		vec3 sample_pos = vec3((VERTEX.xz * WaveScale) + flow, t * 0.07);

		float sample = texture(Noise, sample_pos).r;
		float disp = (sample * 2.0 - 1.0) * WaveAmplitude;

		float strength = WaveProminence * VertexWaveProminence;
		disp *= strength;

		float step_s = 0.05;

		float sample_px = texture(Noise, sample_pos + vec3( step_s, 0.0, 0.0)).r;
		float sample_nx = texture(Noise, sample_pos + vec3(-step_s, 0.0, 0.0)).r;
		float sample_pz = texture(Noise, sample_pos + vec3(0.0, 0.0,  step_s)).r;
		float sample_nz = texture(Noise, sample_pos + vec3(0.0, 0.0, -step_s)).r;

		float disp_px = (sample_px * 2.0 - 1.0) * WaveAmplitude * strength;
		float disp_nx = (sample_nx * 2.0 - 1.0) * WaveAmplitude * strength;
		float disp_pz = (sample_pz * 2.0 - 1.0) * WaveAmplitude * strength;
		float disp_nz = (sample_nz * 2.0 - 1.0) * WaveAmplitude * strength;

		VERTEX.y += disp;

		final_normal = normalize(vec3((disp_nx - disp_px) / (2.0 * step_s), 1.0, (disp_nz - disp_pz) / (2.0 * step_s)));
	}

	NORMAL = final_normal;
}

// Fragment
void fragment() {
	float t = TIME * TimeScale;

	ALBEDO = Color;

	METALLIC = 0.0;
	SPECULAR = SpecularLevel;
	ROUGHNESS = RoughnessLevel;

	// depth fade
	float scene_linear = linearize_depth(texture(Depth, SCREEN_UV).x, SCREEN_UV);
	float water_linear = linearize_depth(FRAGCOORD.z, SCREEN_UV);

	float intersect_dist = scene_linear - water_linear;
	float fade_dist = max(intersect_dist, 0.0);

	ALPHA = (smoothstep(0.0, 4.0, fade_dist) + 1.0) * 0.5;

	// shore calm
	float shore = smoothstep(ShoreCalmStart, ShoreCalmEnd, fade_dist);
	shore = pow(saturate(shore), max(0.001, ShoreCalmPower));

	// ripples (calmed)
	{
		vec2 dir = rocking_dir(t);
		vec2 flow = dir * (t * RippleFlowSpeed);

		vec2 p = v_uv * RippleTiling + flow;

		float e = 0.02;
		float h  = ripple_height(p, t);
		float hx = ripple_height(p + vec2(e, 0.0), t);
		float hz = ripple_height(p + vec2(0.0, e), t);

		float ddx = (hx - h) / e;
		float ddz = (hz - h) / e;

		vec3 ripple_n = normalize(vec3(-ddx * RippleNormalStrength, 1.0, -ddz * RippleNormalStrength));

		float ripple_mix = saturate(RippleBlend * shore * WaveProminence * RippleProminence);
		ripple_mix = min(ripple_mix, MaxRippleMix);

		NORMAL = normalize(mix(NORMAL, ripple_n, ripple_mix));
	}

	// foam
	{
		vec2 dir = rocking_dir(t);
		vec2 foam_flow = dir * (t * 0.55);
		vec3 npos = vec3((v_xz * 0.10) + foam_flow, t * 0.05);
		float n = texture(Noise, npos).r * 2.0 - 1.0;

		float width = max(0.0001, BorderWidth + n * BorderNoise);

		float border = 1.0 - smoothstep(width, width + BorderSoftness, fade_dist);
		border *= step(0.0, intersect_dist);

		float foam_strength = border * BorderIntensity * WaveProminence * FoamProminence;
		ALBEDO = mix(ALBEDO, BorderColor, foam_strength);
	}
}
