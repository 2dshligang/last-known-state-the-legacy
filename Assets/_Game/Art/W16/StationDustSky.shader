Shader "LKS/Sky/Station Dust"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.38, 0.29, 0.265, 1)
        _Horizon ("Horizon", Color) = (0.78, 0.49, 0.36, 1)
        _Ground ("Below Horizon", Color) = (0.30, 0.17, 0.13, 1)
        _SunTint ("Sun Tint", Color) = (1, 0.83, 0.63, 1)
        _SunDirection ("Sun Direction", Vector) = (0.76, 0.60, -0.25, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith;
                half4 _Horizon;
                half4 _Ground;
                half4 _SunTint;
                float4 _SunDirection;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.directionWS = TransformObjectToWorldDir(input.positionOS, false);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 ray = normalize(input.directionWS);
                float elevation = max(0, ray.y);
                half3 sky = lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(elevation * 1.35), 0.62));
                sky = lerp(sky, _Ground.rgb, smoothstep(0.01, 0.29, -ray.y));

                float facingSun = saturate(dot(ray, normalize(_SunDirection.xyz)));
                float halo = pow(facingSun, 10.0) * 0.22 + pow(facingSun, 76.0) * 0.13;
                float disc = smoothstep(0.99970, 0.99986, facingSun) * 0.52;
                sky = lerp(sky, _SunTint.rgb, saturate(halo + disc));
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
}
