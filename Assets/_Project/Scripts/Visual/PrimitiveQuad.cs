using UnityEngine;

// Quad unitario blanco (1u, pivote centrado) COMPARTIDO por los visuales procedurales del boss
// (BossArea, BossSwoosh). Cacheado static -> una sola textura/sprite para todos. Evita duplicar la
// generación del sprite en cada primitivo (DRY). La textura es blanca: el color lo pone el
// SpriteRenderer.color de cada uso.
public static class PrimitiveQuad
{
    private static Sprite unit;

    public static Sprite Unit
    {
        get
        {
            if (unit == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                unit = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
            return unit;
        }
    }
}
