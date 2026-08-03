using UnityEngine;
using UnityEngine.UI;

// Viñeta roja de daño a pantalla completa: al recibir un golpe REAL destella un borde rojo (más
// fuerte en los bordes/esquinas, transparente en el centro) que se desvanece. Feedback de UI clásico.
//
// Autocontenido, al estilo de EndScreenSheetFx/HudBuilder: NO necesita nada en la escena salvo el
// propio componente. En Awake construye por código su Canvas ScreenSpaceOverlay + una Image a pantalla
// completa con un sprite de viñeta radial generado en runtime (sin shaders ni assets). Escucha
// PlayerDamageReceiver.Hit (resuelto por GetComponentInParent, como PlayerHurtFx); el combate no sabe
// que este VFX existe.
public class DamageVignette : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Fuente del evento de daño. Vacío = se resuelve en Awake por GetComponentInParent.")]
    [SerializeField] private PlayerDamageReceiver receiver;

    [Header("Look")]
    [Tooltip("Color del destello (típicamente rojo).")]
    [SerializeField] private Color vignetteColor = new Color(1f, 0.05f, 0.05f, 1f);
    [Tooltip("Opacidad máxima del destello justo al recibir el golpe.")]
    [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.7f;
    [Tooltip("Segundos que tarda en desvanecerse a 0 (tiempo SIN escalar: un golpe mortal congela la partida).")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.4f;

    [Header("Forma de la viñeta (0=centro, 1=esquina)")]
    [Tooltip("Radio donde empieza a aparecer el rojo. Por debajo: transparente (centro limpio).")]
    [SerializeField, Range(0f, 1.5f)] private float innerRadius = 0.3f;
    [Tooltip("Radio donde el rojo llega a su máximo (bordes/esquinas).")]
    [SerializeField, Range(0f, 1.5f)] private float outerRadius = 1f;

    [Header("Render")]
    [Tooltip("Orden del canvas propio: por encima del HUD, por debajo de CanvasPopups (100).")]
    [SerializeField] private int sortingOrder = 90;
    [Tooltip("Resolución del sprite de viñeta generado (px). Basta poca: es un degradado difuso.")]
    [SerializeField] private int textureSize = 128;

    private Image image;
    private float intensity;   // 0..1, decae por frame; se dibuja como alpha del rojo

    private void Awake()
    {
        if (receiver == null) receiver = GetComponentInParent<PlayerDamageReceiver>();
        BuildOverlay();
        Apply();   // arranca invisible
    }

    private void OnEnable()
    {
        if (receiver != null) receiver.Hit += OnHit;
    }

    private void OnDisable()
    {
        if (receiver != null) receiver.Hit -= OnHit;
    }

    private void OnHit(DamageInfo _)
    {
        intensity = 1f;   // pico; Update lo desvanece
        Apply();
    }

    private void Update()
    {
        if (intensity <= 0f) return;
        // Sin escalar: si el golpe mata (timeScale = 0) el rojo igualmente se desvanece, no se queda pegado.
        intensity -= Time.unscaledDeltaTime / fadeDuration;
        if (intensity < 0f) intensity = 0f;
        Apply();
    }

    private void Apply()
    {
        if (image == null) return;
        Color c = vignetteColor;
        c.a = peakAlpha * intensity;
        image.color = c;
        image.enabled = c.a > 0.001f;   // gratis cuando no hay destello
    }

    // --- Construcción por código del overlay ---

    private void BuildOverlay()
    {
        // Hijo del componente para que se destruya con él (sin huérfanos). Un canvas ScreenSpaceOverlay
        // se dibuja a pantalla completa independientemente de su transform padre, así que no hay
        // diferencia visual por colgarlo del jugador (raíz a escala 1).
        var canvasGo = new GameObject("DamageVignetteCanvas", typeof(Canvas));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var imgGo = new GameObject("Vignette", typeof(RectTransform), typeof(Image));
        imgGo.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        image = imgGo.GetComponent<Image>();
        image.sprite = BuildVignetteSprite();
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
        image.enabled = false;
    }

    // Sprite blanco cuyo ALFA crece del centro (0) a las esquinas (1): así, teñido de rojo, el destello
    // es más fuerte en los bordes. RGB blanco para que el tinte del Image mande el color.
    private Sprite BuildVignetteSprite()
    {
        int n = Mathf.Max(8, textureSize);
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = "DamageVignette (runtime)"
        };

        var px = new Color32[n * n];
        Vector2 center = new Vector2(0.5f, 0.5f);
        float cornerDist = center.magnitude;   // distancia centro->esquina (~0.7071), normaliza a [0,1]
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                Vector2 uv = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                float d = Vector2.Distance(uv, center) / cornerDist;   // 0 centro, 1 esquina
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(innerRadius, outerRadius, d));
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
                px[y * n + x] = new Color32(255, 255, 255, alpha);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);

        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}
