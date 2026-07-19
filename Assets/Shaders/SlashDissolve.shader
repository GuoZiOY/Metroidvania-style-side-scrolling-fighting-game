Shader "Custom/SlashDissolve"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _DissolveTex ("Dissolve Texture", 2D) = "white" {}
        _EdgeColor ("Edge Color", Color) = (1, 1, 1, 1)
        _EdgeWidth ("Edge Width", Range(0, 0.5)) = 0.1
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _MainTexOffset ("Main Texture Offset", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _DissolveTex;
            float4 _EdgeColor;
            float _EdgeWidth;
            float _DissolveAmount;
            float2 _MainTexOffset;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv + _MainTexOffset;
                fixed4 mainTex = tex2D(_MainTex, uv);
                fixed dissolveValue = tex2D(_DissolveTex, i.uv).r;

                if (mainTex.a == 0)
                    discard;

                if (dissolveValue < _DissolveAmount)
                    discard;

                if (dissolveValue < _DissolveAmount + _EdgeWidth)
                    return _EdgeColor;

                return mainTex * i.color;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
