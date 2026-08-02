using UnityEngine;

// Escudo heráldico procedural (pentágono con la punta hacia abajo) COMPARTIDO como sprite: borde
// superior plano, lados rectos y convergencia a una punta inferior. Mismo patrón que PrimitiveQuad:
// una sola textura/sprite cacheada static para todos los usos. La textura es blanca con alpha por
// píxel (relleno + borde antialias para conservar el look glow bajo un material aditivo); el color
// lo pone el SpriteRenderer.color de cada uso. Lo usa ParryAura como aura de timing del parry.
public static class PrimitiveShield
{
    private const int TexSize = 256;
    private const float EdgeAA = 2.5f; // ancho del borde antialias, en píxeles

    // Vértices del pentágono heráldico, normalizados a [-0.5, 0.5] con y hacia arriba (pivote
    // centrado). Orden horario. Ajustables aquí para afinar la silueta.
    private static readonly Vector2[] Verts =
    {
        new Vector2(-0.45f, 0.45f),  // superior-izq
        new Vector2( 0.45f, 0.45f),  // superior-der
        new Vector2( 0.45f, -0.05f), // hombro-der
        new Vector2( 0.0f, -0.5f),   // punta (abajo)
        new Vector2(-0.45f, -0.05f), // hombro-izq
    };

    private static Sprite unit;

    public static Sprite Unit
    {
        get
        {
            if (unit == null) unit = Build();
            return unit;
        }
    }

    private static Sprite Build()
    {
        var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "PrimitiveShield (auto)",
        };

        // Polígono en píxeles (mismo origen de coords que la textura: y hacia arriba).
        var poly = new Vector2[Verts.Length];
        for (int i = 0; i < Verts.Length; i++)
            poly[i] = (Verts[i] + new Vector2(0.5f, 0.5f)) * TexSize;

        var pixels = new Color32[TexSize * TexSize];
        for (int y = 0; y < TexSize; y++)
        {
            for (int x = 0; x < TexSize; x++)
            {
                // +0.5 para muestrear el centro del píxel.
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float signed = SignedDistanceToConvex(p, poly); // >0 dentro, <0 fuera
                float a = Mathf.Clamp01(signed / EdgeAA + 0.5f);
                pixels[y * TexSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        // pixelsPerUnit = TexSize -> sprite de ~1u (misma convención que PrimitiveQuad).
        return Sprite.Create(tex, new Rect(0f, 0f, TexSize, TexSize), new Vector2(0.5f, 0.5f), TexSize);
    }

    // Distancia con signo al polígono CONVEXO (vértices en orden horario): positiva dentro,
    // negativa fuera, magnitud = distancia al borde más cercano. Da un borde antialias limpio.
    private static float SignedDistanceToConvex(Vector2 p, Vector2[] poly)
    {
        float minDist = float.MaxValue;
        bool inside = true;

        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % poly.Length];
            Vector2 edge = b - a;

            // Semiplano interior: para orden horario, el interior queda a la derecha del borde
            // (producto cruzado <= 0).
            float cross = edge.x * (p.y - a.y) - edge.y * (p.x - a.x);
            if (cross > 0f) inside = false;

            // Distancia al segmento [a,b].
            float t = Mathf.Clamp01(Vector2.Dot(p - a, edge) / Mathf.Max(edge.sqrMagnitude, 1e-6f));
            float d = Vector2.Distance(p, a + edge * t);
            if (d < minDist) minDist = d;
        }

        return inside ? minDist : -minDist;
    }
}
