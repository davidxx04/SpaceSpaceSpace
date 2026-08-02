// Escudo de energía procedural: dibuja un pentágono heráldico (punta abajo) por SDF dentro del quad
// unitario, con BORDE brillante (rim) + interior translúcido + HALO exterior que sigue la silueta, y
// un pulso sutil. Unlit + additive; el color sale del color de vértice (= SpriteRenderer.color), así
// cada instancia se tinta y su brillo se modula sin material por instancia. Mismo patrón que
// SpaceSpaceSpace/SpriteGlow. Lo usa ParryAura como aura del parry.
//
// (Para builds, añade el shader a *Always Included Shaders* para que Shader.Find sobreviva al stripping.)
Shader "SpaceSpaceSpace/EnergyShield"
{
    Properties
    {
        _RimWidth ("Rim Width", Range(0.005, 0.15)) = 0.03
        _RimIntensity ("Rim Intensity", Range(0, 6)) = 2.0
        _RimWhite ("Rim Whiten", Range(0, 1)) = 0.6
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.25
        _GlowWidth ("Glow Width", Range(0.01, 0.3)) = 0.13
        _GlowPower ("Glow Power", Range(0.5, 6)) = 2.5
        _GlowIntensity ("Glow Intensity", Range(0, 4)) = 1.0
        _PulseSpeed ("Pulse Speed", Range(0, 12)) = 3.0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One   // additive

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            float _RimWidth;
            float _RimIntensity;
            float _RimWhite;
            float _FillAlpha;
            float _GlowWidth;
            float _GlowPower;
            float _GlowIntensity;
            float _PulseSpeed;
            float _PulseAmount;

            // Vértices del escudo heráldico (punta abajo), en espacio UV centrado [-0.5,0.5], con y
            // hacia arriba. Encogidos ~0.68 respecto a la silueta base para dejar margen al halo dentro
            // del quad. Orden del contorno.
            static const float2 SHIELD[5] =
            {
                float2(-0.306,  0.306),  // superior-izq
                float2( 0.306,  0.306),  // superior-der
                float2( 0.306, -0.034),  // hombro-der
                float2( 0.0,   -0.340),  // punta (abajo)
                float2(-0.306, -0.034),  // hombro-izq
            };

            // Distancia con signo al polígono (fórmula de Inigo Quilez): negativa dentro, positiva
            // fuera, magnitud = distancia al borde. El signo sale de la regla par/impar, así no depende
            // del sentido del contorno.
            float sdShield(float2 p)
            {
                float d = dot(p - SHIELD[0], p - SHIELD[0]);
                float s = 1.0;
                [unroll]
                for (int i = 0, j = 4; i < 5; j = i, i++)
                {
                    float2 e = SHIELD[j] - SHIELD[i];
                    float2 w = p - SHIELD[i];
                    float2 b = w - e * clamp(dot(w, e) / dot(e, e), 0.0, 1.0);
                    d = min(d, dot(b, b));

                    bool c1 = p.y >= SHIELD[i].y;
                    bool c2 = p.y <  SHIELD[j].y;
                    bool c3 = (e.x * w.y) > (e.y * w.x);
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
                }
                return s * sqrt(d);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                float d = sdShield(p);            // <0 dentro, >0 fuera
                float aa = max(fwidth(d), 1e-5);

                // Interior translúcido (con AA en el borde).
                float inside = smoothstep(aa, -aa, d);
                float fill = _FillAlpha * inside;

                // Borde brillante alrededor de la silueta.
                float rim = 1.0 - smoothstep(0.0, _RimWidth, abs(d));

                // Halo exterior que sigue la silueta (solo fuera).
                float outside = saturate(d / _GlowWidth);
                float halo = _GlowIntensity * pow(saturate(1.0 - outside), _GlowPower) * step(0.0, d);

                // Pulso sutil de energía.
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                float lum = (rim * _RimIntensity + fill + halo) * pulse;
                lum *= i.color.a;   // tinte/brillo por instancia (ParryAura sube esto para el destello)

                fixed4 col;
                col.rgb = lerp(i.color.rgb, fixed3(1.0, 1.0, 1.0), saturate(rim * _RimWhite));
                col.a = lum;        // additive: rgb*a se suma al fondo
                return col;
            }
            ENDCG
        }
    }
}
