// Shader de UI (Canvas) para el RELLENO de las barras del HUD. Sobre el sprite base (degradado
// teñido por Image.color) añade un brillo VIVO "tipo cáusticas pero sin ser cáusticas": senos
// cruzados animados que forman filamentos de luz en movimiento + un pulso de energía que recorre
// la barra. Todo enmascarado por el alfa del sprite (solo ilumina la parte rellena) y tintado por
// el color de la barra, así un único material vale para vida/energía/boss (cada Image tinta el suyo).
//
// Es UI-compatible (bloque Stencil + clip rect estándar de Canvas). Sin Bloom: el glow se hornea
// aquí (el Canvas del HUD es ScreenSpaceOverlay). Ajusta el feel desde el material HudBarGlow.mat.
Shader "SpaceSpaceSpace/UIBarCaustic"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Tiling ("Caustic Tiling (xy)", Vector) = (10, 3, 0, 0)
        _Speed ("Caustic Speed", Float) = 1.2
        _Sharp ("Caustic Sharpness", Range(1, 8)) = 3
        _GlowIntensity ("Caustic Glow", Range(0, 3)) = 0.9
        _FlowSpeed ("Flow Speed", Float) = 0.25
        _FlowIntensity ("Flow Glow", Range(0, 3)) = 0.5

        // --- Estándar de UI (para máscaras/stencil) ---
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float4 _Tiling;
            float _Speed, _Sharp, _GlowIntensity, _FlowSpeed, _FlowIntensity;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Cáustica falsa: suma de senos cruzados desfasados que se mueven → filamentos brillantes.
            float Caustic(float2 uv, float t)
            {
                float2 p = uv * _Tiling.xy;
                float v = sin(p.x + t) + sin(p.y * 1.3 - t * 0.8) + sin((p.x + p.y) * 0.8 + t * 1.3);
                v = saturate(v / 3.0 * 0.5 + 0.5);   // -> 0..1
                return pow(v, _Sharp);               // afila los filamentos
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 col = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                float t = _Time.y * _Speed;
                float c = Caustic(IN.texcoord, t);

                // pulso de energía que recorre la barra de izquierda a derecha
                float fx = frac(IN.texcoord.x - _Time.y * _FlowSpeed);
                float flow = smoothstep(0.0, 0.5, fx) * smoothstep(1.0, 0.5, fx);

                // brillo aditivo tintado por el color de la barra, solo dentro del relleno (mask = alfa)
                col.rgb += (c * _GlowIntensity + flow * _FlowIntensity) * IN.color.rgb * col.a;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
