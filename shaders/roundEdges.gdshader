shader_type canvas_item;

// Per-corner radii in normalized UV (0.0–0.5 is sane)
uniform float radius_tl = 0.15; // top-left
uniform float radius_tr = 0.15; // top-right
uniform float radius_br = 0.15; // bottom-right
uniform float radius_bl = 0.15; // bottom-left

// How soft the edge is (anti-alias)
uniform float edge_softness : hint_range(0.0, 3.0) = 1.0;

void fragment() {
    vec2 uv = UV;
    vec4 tex = texture(TEXTURE, uv);

    // if image is fully transparent anyway, no need to do anything
    if (tex.a <= 0.0) {
        discard;
    }

    float alpha_mask = 1.0;

    // Helper for one corner
    // uv_center = circle center in UV
    // r = radius in UV
    // inside = 1, outside = 0, smooth on the edge
    float aa, dist, m;

    // TOP-LEFT
    if (radius_tl > 0.0 && uv.x < radius_tl && uv.y < radius_tl) {
        vec2 center = vec2(radius_tl, radius_tl);
        dist = length(uv - center);
        float r = radius_tl;
        aa = max(fwidth(dist) * edge_softness, 0.0001);
        m = 1.0 - smoothstep(r, r + aa, dist);
        alpha_mask *= m;
    }

    // TOP-RIGHT
    if (radius_tr > 0.0 && uv.x > 1.0 - radius_tr && uv.y < radius_tr) {
        vec2 center = vec2(1.0 - radius_tr, radius_tr);
        dist = length(uv - center);
        float r = radius_tr;
        aa = max(fwidth(dist) * edge_softness, 0.0001);
        m = 1.0 - smoothstep(r, r + aa, dist);
        alpha_mask *= m;
    }

    // BOTTOM-RIGHT
    if (radius_br > 0.0 && uv.x > 1.0 - radius_br && uv.y > 1.0 - radius_br) {
        vec2 center = vec2(1.0 - radius_br, 1.0 - radius_br);
        dist = length(uv - center);
        float r = radius_br;
        aa = max(fwidth(dist) * edge_softness, 0.0001);
        m = 1.0 - smoothstep(r, r + aa, dist);
        alpha_mask *= m;
    }

    // BOTTOM-LEFT
    if (radius_bl > 0.0 && uv.x < radius_bl && uv.y > 1.0 - radius_bl) {
        vec2 center = vec2(radius_bl, 1.0 - radius_bl);
        dist = length(uv - center);
        float r = radius_bl;
        aa = max(fwidth(dist) * edge_softness, 0.0001);
        m = 1.0 - smoothstep(r, r + aa, dist);
        alpha_mask *= m;
    }

    float final_alpha = tex.a * alpha_mask;

    if (final_alpha <= 0.0) {
        discard; // hard cut outside rounded rect
    }

    COLOR = vec4(tex.rgb, final_alpha);
}
