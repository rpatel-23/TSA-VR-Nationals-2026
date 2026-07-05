// Hologram_URP.shader — DECRYPTED
// Used on RevealSculpture stage renderers. Supports the _Dissolve float that
// FinalRevealController drives via MaterialPropertyBlock, plus scanline FX
// and Fresnel rim glow. Quest-safe: single pass, no GrabPass, no depth-write.

Shader "DECRYPTED/Hologram_URP"
{
    Properties
    {
        _BaseColor      ("Base Color",     Color)  = (0.2, 0.9, 1.0, 1.0)
        _EmissionColor  ("Emission Color", Color)  = (0.2, 0.9, 1.0, 1.0)
        _EmissionStrength("Emission Strength", Float) = 1.0
        _Dissolve       ("Dissolve",       Range(0,1)) = 0.0
        _ScanlineFreq   ("Scanline Freq",  Float)  = 40.0
        _ScanlineSpeed  ("Scanline Speed", Float)  = 0.8
        _ScanlineStrength("Scanline Strength", Range(0,1)) = 0.35
        _FresnelPow     ("Fresnel Power",  Float)  = 3.0
        _FresnelStrength("Fresnel Strength", Float) = 1.2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                half4  _EmissionColor;
                float  _EmissionStrength;
                float  _Dissolve;
                float  _ScanlineFreq;
                float  _ScanlineSpeed;
                float  _ScanlineStrength;
                float  _FresnelPow;
                float  _FresnelStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vpi.positionCS;
                OUT.positionWS  = vpi.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogFactor   = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Scanlines scrolling on world Y
                float scan = sin(IN.positionWS.y * _ScanlineFreq + _Time.y * _ScanlineSpeed * 6.2832);
                float scanMask = lerp(1.0, saturate(scan * 0.5 + 0.5), _ScanlineStrength);

                // Fresnel rim
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float  NdotV   = saturate(dot(normalize(IN.normalWS), viewDir));
                float  fresnel = pow(1.0 - NdotV, _FresnelPow) * _FresnelStrength;

                // Dissolve clips fragments from the top down
                float clipThreshold = (1.0 - _Dissolve) * 2.0 - 1.0;
                float normY = IN.positionWS.y * 0.5; // rough normalisation
                clip(normY - clipThreshold + 0.001);

                half3 emission = _EmissionColor.rgb * _EmissionStrength;
                half3 col = _BaseColor.rgb * scanMask + emission + fresnel * _EmissionColor.rgb;
                half  a   = _BaseColor.a * scanMask * (1.0 - _Dissolve) + fresnel * 0.6;

                half4 result = half4(col, saturate(a));
                result.rgb = MixFog(result.rgb, IN.fogFactor);
                return result;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
