// Source ed82e4186748 SkyArea PS b000003.
// SHA256 375a2141e08763a80e55605460a335782442e2d613342ca1836cbc345c6485f7.
// Shared by Unity and the native-GPU comparison; expected pixels execute the
// ORIGINAL bytecode, not this implementation.
#ifndef SPC_SOURCE_SKY_AREA_INCLUDED
#define SPC_SOURCE_SKY_AREA_INCLUDED
float2 SpcSkyGradient(int2 cell)
{
    uint seed=asuint(cell.y)^0x41c64e6du;
    uint hash=seed*(seed+asuint(cell.x));
    hash^=hash>>5;
    hash*=0x27d4eb2du;
    float randomValue=(hash>>8)*asfloat(0x33800001u);
    float2 gradient=float2(randomValue-floor(randomValue+0.5),randomValue-0.5);
    return gradient*rsqrt(dot(gradient,gradient));
}
float SpcSkyNoise(float2 position)
{
    int2 cell=(int2)floor(position);
    float2 f=frac(position);
    float2 weight=f*f*f*(f*(f*6.0-15.0)+10.0);
    float n00=dot(SpcSkyGradient(cell),f);
    float n01=dot(SpcSkyGradient(cell+int2(0,1)),f-float2(0,1));
    float n10=dot(SpcSkyGradient(cell+int2(1,0)),f-float2(1,0));
    float n11=dot(SpcSkyGradient(cell+int2(1,1)),f-float2(1,1));
    // Preserve source interpolation order: y, then x.
    return lerp(lerp(n00,n01,weight.y),lerp(n10,n11,weight.y),weight.x);
}
float3 SpcSkyGamma(float3 value)
{
    float3 high=exp2(log2(abs(value))*asfloat(0x3ed55555u))*asfloat(0x3f870a3du)+asfloat(0xbd6147aeu);
    return float3(value.x<=asfloat(0x3b4d2e1cu)?value.x*asfloat(0x414ec578u):high.x,
        value.y<=asfloat(0x3b4d2e1cu)?value.y*asfloat(0x414ec578u):high.y,
        value.z<=asfloat(0x3b4d2e1cu)?value.z*asfloat(0x414ec578u):high.z);
}
float SpcSkyOverlapDepth(float absoluteX,float2 bounds)
{
    float halfWidth=(bounds.y-bounds.x)*0.5;
    float distance=abs(absoluteX-((bounds.y-bounds.x)*0.5+bounds.x));
    return distance>=halfWidth?0.0:(halfWidth-distance)*1.62;
}
float4 SpcSourceSkyArea(float2 uv0,float2 uv1,float2 uv2,float worldZ,
    float visualStart,float visualProgress,float first,float last,float4 missingRange,
    float4 mainColor,float4 edgeCenter,float4 edgeGlow)
{
    float distance=uv1.y+visualStart-visualProgress;
    float3 phases=distance*float3(0.08,0.1,0.05)+visualProgress*float3(0.008,0.007,0.006);
    float noiseAlpha=(SpcSkyNoise(float2(uv0.x,phases.y)*1.78)+0.5)*0.4;
    float textureFade=max(5.0-distance*0.6,0.0);
    float distanceScale=min(max((distance-12.0)*asfloat(0xbdaaaaabu),0.1),1.0);
    float arch=SPC_SKY_SAMPLE(_ArchTexture,float2(uv0.x,phases.x)).a;
    float dots=SPC_SKY_SAMPLE(_DotTexture,float2(uv0.x,phases.z)).a;
    float bodyAlpha=max(max(noiseAlpha,0.2),max(arch*min(textureFade,1.3),0.0))*mainColor.a;
    float dotAlpha=dots*min(textureFade,1.0)*noiseAlpha;
    float3 premultiplied=bodyAlpha*mainColor.rgb*(1.0-dotAlpha)+
        dotAlpha*float3(asfloat(0x3f71cef7u),asfloat(0x3f55bc60u),1.0);
    float alpha=bodyAlpha*(1.0-dotAlpha)+dotAlpha;
    float endDistance=(uv2.y-uv1.y)*distanceScale;
    if(last<0.5)endDistance+=SpcSkyOverlapDepth(uv0.x,missingRange.zw);
    float startDistance=first>0.5?1.0:uv1.y*distanceScale;
    if(first<0.5)startDistance+=SpcSkyOverlapDepth(uv0.x,missingRange.xy);
    float side=uv1.x*uv2.x*1.62;
    side=min(side,uv2.x*1.62-side);
    float edge=min(2.0*saturate(0.48-min(side,min(startDistance,endDistance))),1.0);
    bool isOuter=edge>=0.92;
    float outer=(asfloat(0x3da3d708u)-(edge-0.92))*max((worldZ+8.8)*0.4,1.7)*asfloat(0x41480003u);
    edge=min(isOuter?outer:edge,1.0);
    float coverage=max(isOuter?0.0:1.0,edge);
    float edgeWeight=exp2(log2(edge)*(7.0-(SpcSkyNoise(worldZ.xx)+0.5)*0.5));
    float4 edgeColor=float4(lerp(SpcSkyGamma(edgeGlow.rgb),SpcSkyGamma(edgeCenter.rgb),edgeWeight),
        lerp(edgeGlow.a,edgeCenter.a,edgeWeight));
    premultiplied=premultiplied*(1.0-edgeColor.a)+edgeColor.rgb*edgeColor.a;
    alpha=alpha*(1.0-edgeColor.a)+edgeColor.a;
    float3 color=premultiplied/alpha;
    float luminance=dot(color,float3(asfloat(0x3e59c6edu),asfloat(0x3f371437u),asfloat(0x3d93d07du)));
    return float4((color-luminance)*2.0+luminance,coverage*saturate(alpha));
}
#endif
