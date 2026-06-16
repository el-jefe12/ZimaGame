shader_type canvas_item;

uniform vec4 from_color : source_color;   // color to replace
uniform vec4 to_color   : source_color;   // target color
uniform float tolerance = 0.05;           // how close colors must be

uniform bool affect_alpha = false;        // toggle alpha modification
uniform float alpha_strength : hint_range(0.0, 1.0) = 1.0; // 0..1 blend

uniform bool flip_h = false;
uniform bool flip_v = false;

// --- circle mask stuff (optional) ---
uniform bool use_circle_mask = false;                 // enable/disable soft circle
uniform vec2 circle_center = vec2(0.5, 0.5);          // center in UV space
uniform float circle_radius : hint_range(0.0, 1.0) = 0.5;     // radius in UV units
uniform float edge_softness : hint_range(0.0, 0.5) = 0.05;    // how soft the edge is

// Smooth-ish point sampling helper
vec4 texturePointSmooth(sampler2D smp, vec2 uv, vec2 pixel_size)
{
    vec2 ddx = dFdx(uv);
    vec2 ddy = dFdy(uv);
    vec2 lxy = sqrt(ddx * ddx + ddy * ddy);

    // avoid division by zero
    lxy = max(lxy, pixel_size);

    vec2 uv_pixels = uv / pixel_size;

    vec2 uv_pixels_floor = round(uv_pixels) - vec2(0.5);
    vec2 uv_dxy_pixels = uv_pixels - uv_pixels_floor;

    uv_dxy_pixels = clamp(
        (uv_dxy_pixels - vec2(0.5)) * pixel_size / lxy + vec2(0.5),
        0.0,
        1.0
    );

    uv = uv_pixels_floor * pixel_size;

    return textureGrad(smp, uv + uv_dxy_pixels * pixel_size, ddx, ddy);
}

void fragment() {
    // base UV
    vec2 uv = UV;

    // apply flips
    if (flip_h) {
        uv.x = 1.0 - uv.x;
    }
    if (flip_v) {
        uv.y = 1.0 - uv.y;
    }

    // sample once (with your smooth function)
    vec4 tex = texturePointSmooth(TEXTURE, uv, TEXTURE_PIXEL_SIZE);
    // if you don't care about smooth sampling, you can just do:
    // vec4 tex = texture(TEXTURE, uv);

    // default = original
    vec4 result = tex;

    // color difference
    float diff = distance(tex.rgb, from_color.rgb);

    // only modify pixels that match the source color (within tolerance)
    if (diff < tolerance && tex.a > 0.0) {
        // change RGB
        result.rgb = to_color.rgb;

        // optional alpha handling
        if (affect_alpha) {
            // new alpha based on target color's alpha
            float new_alpha = tex.a * to_color.a;
            // blend between original and new alpha by alpha_strength
            result.a = mix(tex.a, new_alpha, alpha_strength);
        } else {
            // keep original alpha
            result.a = tex.a;
        }
    }

    // --- apply optional circular soft mask ---
    if (use_circle_mask) {
        // distance from circle center in UV space
        float dist = distance(UV, circle_center);

        // smooth mask: 1 in center, fades to 0 at radius
        float inner = circle_radius - edge_softness;
        inner = max(inner, 0.0); // avoid negative inner radius

        float mask = 1.0 - smoothstep(inner, circle_radius, dist);

        // multiply alpha by mask (outside circle -> transparent)
        result.a *= mask;
    }

    COLOR = result;
}
