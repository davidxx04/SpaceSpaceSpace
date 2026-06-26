using UnityEngine;

// Glow procedural REUTILIZABLE: dibuja un halo radial additive (detrás del sprite anfitrión) tintable
// por color. Genera en código su material (shader SpaceSpaceSpace/SpriteGlow) y su sprite (cuadrado
// unitario), cacheados static y compartidos — mismo patrón que AfterimageEmitter, así no necesita
// arte ni cableado de material. Sirve para balas (código de color rojo/cian) y luego para áreas/swooshes.
//
// (Para builds, añade el shader a *Always Included Shaders* para que Shader.Find sobreviva al stripping.)
[DisallowMultipleComponent]
public class SpriteGlow : MonoBehaviour
{
    [Tooltip("Renderer del halo. Si está vacío, se crea un hijo 'Glow' en Awake.")]
    [SerializeField] private SpriteRenderer glowRenderer;

    [Tooltip("Diámetro aprox. del halo, en unidades de mundo.")]
    [SerializeField] private float radius = 1.2f;

    [Tooltip("Recoloca el brillo respecto al centro del objeto (X,Y) si el sprite no está centrado.")]
    [SerializeField] private Vector2 offset = Vector2.zero;

    [Tooltip("Color inicial del glow (lo sobreescribe SetColor en runtime).")]
    [SerializeField] private Color color = Color.white;

    [Tooltip("Orden de dibujado relativo al sprite anfitrión (negativo = detrás).")]
    [SerializeField] private int sortingOffset = -1;

    [Header("Pulso (opcional)")]
    [SerializeField] private bool pulse = false;
    [SerializeField] private float pulseSpeed = 4f;
    [SerializeField] private float pulseAmount = 0.15f;

    private static Material glowMaterial;
    private static Sprite quadSprite;

    private float baseAlpha;

    private void Awake()
    {
        if (glowRenderer == null) glowRenderer = CreateHalo();
        ApplyColor();
        ApplySize();
        ApplyOffset();
    }

    // Permite tunear color/tamaño/offset EN VIVO desde el Inspector durante el Play (el halo ya existe).
    private void OnValidate()
    {
        if (glowRenderer == null) return;
        ApplyColor();
        ApplySize();
        ApplyOffset();
    }

    // Tinta el glow en runtime (para áreas/swooshes que cambian de color). Las balas hornean su color
    // en el campo 'color' del Inspector (cada prefab el suyo: rojo/cian), así que no necesitan llamarlo.
    public void SetColor(Color c)
    {
        color = c;
        ApplyColor();
    }

    public void SetRadius(float r)
    {
        radius = r;
        ApplySize();
    }

    public void SetOffset(Vector2 o)
    {
        offset = o;
        ApplyOffset();
    }

    private void Update()
    {
        if (!pulse || glowRenderer == null) return;
        float k = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        Color c = color;
        c.a = baseAlpha * k;
        glowRenderer.color = c;
    }

    private void ApplyColor()
    {
        baseAlpha = color.a;
        if (glowRenderer != null) glowRenderer.color = color;
    }

    private void ApplySize()
    {
        if (glowRenderer == null) return;
        // El sprite unitario mide 1u; escalamos al diámetro deseado.
        glowRenderer.transform.localScale = Vector3.one * Mathf.Max(0.001f, radius);
    }

    private void ApplyOffset()
    {
        if (glowRenderer != null) glowRenderer.transform.localPosition = offset;
    }

    private SpriteRenderer CreateHalo()
    {
        var go = new GameObject("Glow");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetQuadSprite();
        sr.sharedMaterial = GetGlowMaterial();

        // Se ordena respecto al sprite anfitrión si lo hay (si no, usa solo el offset).
        if (TryGetComponent<SpriteRenderer>(out var host))
        {
            sr.sortingLayerID = host.sortingLayerID;
            sr.sortingOrder = host.sortingOrder + sortingOffset;
        }
        else
        {
            sr.sortingOrder = sortingOffset;
        }
        return sr;
    }

    private static Material GetGlowMaterial()
    {
        if (glowMaterial == null)
        {
            Shader shader = Shader.Find("SpaceSpaceSpace/SpriteGlow");
            if (shader != null) glowMaterial = new Material(shader) { name = "SpriteGlow (auto)" };
        }
        return glowMaterial;
    }

    private static Sprite GetQuadSprite()
    {
        if (quadSprite == null)
        {
            Texture2D tex = Texture2D.whiteTexture;
            // Sprite de 1 unidad (pixelsPerUnit = ancho de la textura), pivote centrado. El shader usa
            // el UV para la caída radial; la textura en sí no importa (es blanca).
            quadSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }
        return quadSprite;
    }
}
