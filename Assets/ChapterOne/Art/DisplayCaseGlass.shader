Shader "MatchaCloud/DisplayCaseGlass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Existing cabinet sprite", 2D) = "white" {}
        _GlassOpacity ("Glass tint", Range(0,1)) = 0.12
        _ReflectionOpacity ("Reflection", Range(0,1)) = 0.38
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half _GlassOpacity, _ReflectionOpacity;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS); o.uv=v.uv; o.color=v.color; return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;
                // Keep the original dark frame/shelf opaque. Reuse the original
                // pale glass and white reflection pixels as a translucent foreground.
                half3 palette=c.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                palette=LinearToSRGB(palette);
                #endif
                half light=min(palette.r,min(palette.g,palette.b));
                half opacity=light<0.55h ? 1.0h : (light>0.88h ? _ReflectionOpacity : _GlassOpacity);
                c.a*=opacity; return c;
            }
            ENDHLSL
        }
    }
}
