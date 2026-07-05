// EnergyScanline_URP.shader — DECRYPTED
// Used on Vault interior panels and archive glow. Animated vertical energy
// scan that sweeps continuously. Quest-safe: additive blend, no GrabPass.

Shader "DECRYPTED/EnergyScanline_URP"
{
    Properties
    {
        _BaseColor      ("Base Color",      Color)  = (0.2, 0.9, 1.0, 1.0)
        _EmissionColor  ("Emission Color",  Color)  = (0.2, 0.9, 1.0, 1.0)
        _EmissionStrength("Emission Strength", Float) = 1.5
        _ScanSpeed      ("Scan Speed",      Float)  = 1.2
        _ScanWidth      ("Scan Band Width", Range(0.01, 0.5)) = 0.12
        _ScanBrightness ("Scan Brightness", Float)  = 2.5
        _GridFreqX      ("Grid Freq X",     Float)  = 8.0
        _GridFreqY      ("Grid Freq Y",     Float)  = 20.0
        _GridStrength   ("Grid Strength",   Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Blend One One         // additive — glows onto whatever is behind
        ZWrite Off
        Cull Back

        Pass
        {
            Name "EnergyScanline"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                half4  _EmissionColor;
                float  _EmissionStrength;
                float  _ScanSpeed;
                float  _ScanWidth;
                float  _ScanBrightness;
                float  _GridFreqX;
                float  _GridFreqY;
                float  _GridStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = vpi.positionCS;
                OUT.positionWS  = vpi.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Animated scan band sweeping up the surface
                float scanPos = frac(_Time.y * _ScanSpeed);
                float dist    = abs(IN.uv.y - scanPos);
                float band    = saturate(1.0 - dist / _ScanWidth);
                band = band * band; // sharpen falloff

                // Subtle grid overlay
                float gx = abs(sin(IN.uv.x * _GridFreqX * 3.14159));
                float gy = abs(sin(IN.uv.y * _GridFreqY * 3.14159));
                float grid = saturate(max(gx, gy));
                grid = lerp(0.0, grid, _GridStrength);

                // Fresnel edge glow
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float  NdotV   = saturate(dot(normalize(IN.normalWS), viewDir));
                float  fresnel = pow(1.0 - NdotV, 2.5);

                half3 base = _BaseColor.rgb * _EmissionStrength;
                half3 scan = _EmissionColor.rgb * band * _ScanBrightness;
                half3 col  = base + scan + _EmissionColor.rgb * (grid + fresnel * 0.6);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
