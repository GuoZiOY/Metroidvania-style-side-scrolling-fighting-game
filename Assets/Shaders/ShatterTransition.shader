// 场景切换黑幕过渡 shader（内置管线）— 黑幕（颜色取 UI 顶点色，灰罩→黑渐变）+ 四周向中心收敛 / 中心向外淡出：
// _Cover 0→1：黑幕从四周向中心淡入（进入，与淡出对称）；_Reveal 0→1：黑幕从中心向四周淡出（显现新场景，不愈合）。
// 配合黑边/区域名等电影化框架使用。
Shader "Custom/ShatterTransition"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Cover ("Cover To Center", Range(0, 1)) = 0
        _Reveal ("Reveal From Center", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Cover;
            float _Reveal;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 中心向外透明淡出（_Reveal 0→1：中心先透明，向边缘扩散）——用于显现新场景，不愈合
                float centerDist = length(i.uv - 0.5);
                float revealEdge = _Reveal * 0.85;
                float revealAlpha = 1.0 - smoothstep(0.0, 0.08, revealEdge - centerDist);

                // 四周向中心淡入（_Cover 0→1：边缘先覆盖、向中心收敛）——用于黑幕进入，与淡出反向对称
                // 0.8 略大于屏幕角点到中心的归一化距离(≈0.707)，保证 _Cover=0 时全屏透明
                float coverEdge = (1.0 - _Cover) * 0.8;
                float coverAlpha = 1.0 - smoothstep(0.0, 0.08, coverEdge - centerDist);

                // 黑幕颜色取自 UI 顶点色（SceneTransitionFader 驱动灰罩色 → 黑色渐变）
                return fixed4(i.color.rgb, i.color.a * coverAlpha * revealAlpha);
            }
            ENDCG
        }
    }
}
