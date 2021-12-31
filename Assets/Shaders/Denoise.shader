// a runtime denoiser
// reference: https://www.shadertoy.com/view/ldKBzG
// https://gist.github.com/pissang/fc5688ce9a544947e0cea060efec610f
Shader "Hidden/Denoise"
{
    Properties
    {
        _MainTex("Color Texture", 2D) = "white" {}
    }
        SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Off Blend Off
        //Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            sampler2D _NormTex;
            float2 _StepSize;
            float4 _Coeff;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f inData) : SV_Target
            {
                //prepare offset
                float2 offset[25];
                offset[0] = float2(-2,-2);
                offset[1] = float2(-1,-2);
                offset[2] = float2(0,-2);
                offset[3] = float2(1,-2);
                offset[4] = float2(2,-2);

                offset[5] = float2(-2,-1);
                offset[6] = float2(-1,-1);
                offset[7] = float2(0,-1);
                offset[8] = float2(1,-1);
                offset[9] = float2(2,-1);

                offset[10] = float2(-2,0);
                offset[11] = float2(-1,0);
                offset[12] = float2(0,0);
                offset[13] = float2(1,0);
                offset[14] = float2(2,0);

                offset[15] = float2(-2,1);
                offset[16] = float2(-1,1);
                offset[17] = float2(0,1);
                offset[18] = float2(1,1);
                offset[19] = float2(2,1);

                offset[20] = float2(-2,2);
                offset[21] = float2(-1,2);
                offset[22] = float2(0,2);
                offset[23] = float2(1,2);
                offset[24] = float2(2,2);

                // prepare kernel
                float kernel[25];
                kernel[0] = 1.0f / 256.0f;
                kernel[1] = 1.0f / 64.0f;
                kernel[2] = 3.0f / 128.0f;
                kernel[3] = 1.0f / 64.0f;
                kernel[4] = 1.0f / 256.0f;

                kernel[5] = 1.0f / 64.0f;
                kernel[6] = 1.0f / 16.0f;
                kernel[7] = 3.0f / 32.0f;
                kernel[8] = 1.0f / 16.0f;
                kernel[9] = 1.0f / 64.0f;

                kernel[10] = 3.0f / 128.0f;
                kernel[11] = 3.0f / 32.0f;
                kernel[12] = 9.0f / 64.0f;
                kernel[13] = 3.0f / 32.0f;
                kernel[14] = 3.0f / 128.0f;

                kernel[15] = 1.0f / 64.0f;
                kernel[16] = 1.0f / 16.0f;
                kernel[17] = 3.0f / 32.0f;
                kernel[18] = 1.0f / 16.0f;
                kernel[19] = 1.0f / 64.0f;

                kernel[20] = 1.0f / 256.0f;
                kernel[21] = 1.0f / 64.0f;
                kernel[22] = 3.0f / 128.0f;
                kernel[23] = 1.0f / 64.0f;
                kernel[24] = 1.0f / 256.0f;

                float3 sum = 0.0;
                float c_phi = _Coeff.x;
                float n_phi = _Coeff.y;
                float p_phi = _Coeff.z;
                float strength = _Coeff.w;
                float3 cval = saturate(tex2D(_MainTex, inData.uv).rgb);
                float4 npval = tex2D(_NormTex, inData.uv);
                float3 nval = npval.xyz; // normal
                float pval = npval.w; // depth
                if (pval == 1.#INF)
                    return float4(cval, 1.0);

                float sum_w = 0.0;
                for (int i = 0; i < 25; i++)
                {
                    float2 uv = inData.uv + offset[i] * _StepSize * strength;

                    float3 ctmp = saturate(tex2D(_MainTex, uv).rgb);
                    float4 nptmp = tex2D(_NormTex, uv);
                    if (nptmp.w == 1.#INF)
                        continue;

                    float3 t = cval - ctmp;
                    float dist2 = dot(t, t);
                    float c_w = min(exp(-(dist2) / c_phi), 1.0);            

                    float3 ntmp = nptmp.xyz;
                    t = nval - ntmp;
                    dist2 = max(dot(t, t), 0.0);
                    float n_w = min(exp(-(dist2) / n_phi), 1.0);

                    float ptmp = nptmp.w;
                    t.x = pval - ptmp;
                    dist2 = t.x * t.x;
                    float p_w = min(exp(-(dist2) / p_phi), 1.0);

                    float weight = c_w * n_w * p_w * kernel[i];
                    sum += ctmp * weight;
                    sum_w += weight;
                }
                return float4(sum / sum_w, 1.0);
            }
            ENDCG
        }
    }
}
