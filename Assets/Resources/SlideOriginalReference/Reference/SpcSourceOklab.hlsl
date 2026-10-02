// Game.Common/$I.$cf: $KBA -> $LBA -> $lBA. The source operates on the raw
// SafeAreaColorDetails Color components before Material.SetColor. This project's
// source-reference runtime is Gamma: do not add a second sRGB/linear transform.
#ifndef SPC_SOURCE_OKLAB_INCLUDED
#define SPC_SOURCE_OKLAB_INCLUDED

float SpcSourceCbrt(float value)
{
    // Math.Cbrt in the original also accepts zero and negative values.
    return value == 0.0 ? 0.0 : sign(value) * pow(abs(value), 1.0 / 3.0);
}

float3 SpcSourceColorToOklab(float3 color)
{
    float l = SpcSourceCbrt(0.41222146 * color.r + 0.53633255 * color.g + 0.051445995 * color.b);
    float m = SpcSourceCbrt(0.2119035 * color.r + 0.6806995 * color.g + 0.10739696 * color.b);
    float s = SpcSourceCbrt(0.08830246 * color.r + 0.28171885 * color.g + 0.6299787 * color.b);
    return float3(0.21045426 * l + 0.7936178 * m - 0.004072047 * s,
                  1.9779985 * l - 2.4285922 * m + 0.4505937 * s,
                  0.025904037 * l + 0.78277177 * m - 0.80867577 * s);
}

float3 SpcSourceOklabToColor(float3 lab)
{
    float l = lab.x + 0.39633778 * lab.y + 0.21580376 * lab.z;
    float m = lab.x - 0.105561346 * lab.y - 0.06385417 * lab.z;
    float s = lab.x - 0.08948418 * lab.y - 1.2914855 * lab.z;
    l = l * l * l; m = m * m * m; s = s * s * s;
    return float3(4.0767417 * l - 3.3077116 * m + 0.23096994 * s,
                  -1.268438 * l + 2.6097574 * m - 0.34131938 * s,
                  -0.0041960864 * l - 0.7034186 * m + 1.7076147 * s);
}

float4 SpcSourceOklabLerp(float4 from, float4 to, float weight)
{
    // Exact endpoints keep the fragment-only oracle usable with pre-mixed colors.
    // Original CPU matrix round-trips differ only by float rounding at these ends.
    if (weight <= 0.0) return from;
    if (weight >= 1.0) return to;
    float3 first = SpcSourceColorToOklab(from.rgb);
    float3 second = SpcSourceColorToOklab(to.rgb);
    return float4(SpcSourceOklabToColor(first * (1.0 - weight) + second * weight),
                  lerp(from.a, to.a, weight));
}
#endif
