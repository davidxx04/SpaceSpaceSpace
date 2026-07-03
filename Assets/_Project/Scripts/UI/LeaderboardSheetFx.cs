using UnityEngine;
using UnityEngine.UI;

// Efectos retro de la hoja del leaderboard, construidos POR CÓDIGO en runtime (sin builder, sin
// tocar la escena): LeaderboardPopup.Awake llama a las dos factorías estáticas según sus toggles.
// Cada efecto es independiente y se quita borrando su llamada (o desmarcando el bool del popup):
//
//  - CreateSpectrum: "SpectrumBg", Image a pantalla completa entre el Dim y el contenido, con el
//    shader SpaceSpaceSpace/SpectrumSheet (abanico de palas arcoíris + grano de película). Este
//    MonoBehaviour vive SOLO en ese objeto: posee la instancia de material y le empuja _Size del
//    RectTransform (la UI no soporta MaterialPropertyBlocks — mismo patrón que NeonButtonFx).
//  - CreateStripes: "PolaroidStripes", banda de 5 franjas planas (paleta Polaroid clásica) centrada
//    en el hueco entre la cabecera y la lista. Solo Images; no necesita componente.
//
// Los objetos son hijos de Panel_Leaderboard, así que heredan el alpha de su CanvasGroup y se
// muestran/ocultan con el popup sin código extra. El whitelist de LeaderboardBuilder es editor-only:
// estos hijos runtime no chocan con él.
public class LeaderboardSheetFx : MonoBehaviour
{
    // Copia de la geometría de LeaderboardBuilder (Editor-only, inaccesible desde runtime).
    private const float SideMargin = 48f;
    private const float TopMargin = 40f;
    private const float HeaderHeight = 50f;
    private const float HeaderGap = 10f;
    private const float StripeHeight = 8f;

    // Paleta de la etiqueta Polaroid clásica, de arriba a abajo.
    private static readonly Color[] PolaroidColors =
    {
        new Color32(0x01, 0xA6, 0x51, 0xFF),   // verde
        new Color32(0xFD, 0xB9, 0x13, 0xFF),   // amarillo
        new Color32(0xF5, 0x82, 0x20, 0xFF),   // naranja
        new Color32(0xEF, 0x41, 0x35, 0xFF),   // rojo
        new Color32(0x00, 0x95, 0xDA, 0xFF),   // azul
    };

    private static readonly int SizeId = Shader.PropertyToID("_Size");

    private Image image;
    private Material mat;

    // --- Abanico espectral + grano ---

    public static void CreateSpectrum(RectTransform panel)
    {
        if (panel == null || panel.Find("SpectrumBg") != null) return;

        var go = new GameObject("SpectrumBg", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(panel, false);
        Stretch((RectTransform)go.transform);
        go.transform.SetSiblingIndex(1);   // encima del Dim (0), debajo de Header/ScrollView

        var img = go.GetComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = false;

        go.AddComponent<LeaderboardSheetFx>();
    }

    private void Awake()
    {
        image = GetComponent<Image>();

        Shader shader = Shader.Find("SpaceSpaceSpace/SpectrumSheet");
        if (shader == null)
        {
            // Fail-safe: sin shader (¿stripping?), el efecto simplemente no existe.
            Debug.LogWarning("[LeaderboardSheetFx] No encuentro 'SpaceSpaceSpace/SpectrumSheet'. " +
                "¿Está en Always Included Shaders (Project Settings > Graphics)?");
            Destroy(gameObject);
            return;
        }

        mat = new Material(shader) { name = "SpectrumSheet (runtime)" };
        image.material = mat;
        PushSize();
    }

    // En el Awake de carga de escena el canvas aún no ha maquetado (rect puede ser 0): reintenta.
    private void Start() => PushSize();

    private void OnRectTransformDimensionsChange() => PushSize();

    private void OnDestroy()
    {
        if (mat != null) Destroy(mat);   // la escena Menu se recarga en cada vuelta al menú
    }

    // El shader corrige el aspecto con el tamaño real del rect (el abanico no se estira).
    private void PushSize()
    {
        if (mat == null) return;
        Vector2 size = ((RectTransform)transform).rect.size;
        if (size.x < 1f || size.y < 1f) return;   // layout aún no resuelto: quedan los defaults
        mat.SetVector(SizeId, size);
    }

    // --- Franjas Polaroid ---

    public static void CreateStripes(RectTransform panel)
    {
        if (panel == null || panel.Find("PolaroidStripes") != null) return;

        // Banda centrada en el hueco entre la cabecera y la lista (mismas medidas que el builder).
        float top = -(TopMargin + HeaderHeight + (HeaderGap - StripeHeight) * 0.5f);

        var band = new GameObject("PolaroidStripes", typeof(RectTransform));
        band.transform.SetParent(panel, false);
        var rt = (RectTransform)band.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(SideMargin, top - StripeHeight);
        rt.offsetMax = new Vector2(-SideMargin, top);
        band.transform.SetAsLastSibling();

        // 5 franjas planas por anchors fraccionales: exactas, crujientes y sin layout rebuilds.
        for (int i = 0; i < PolaroidColors.Length; i++)
        {
            var stripe = new GameObject("Stripe" + i, typeof(RectTransform), typeof(Image));
            stripe.transform.SetParent(band.transform, false);
            var srt = (RectTransform)stripe.transform;
            srt.anchorMin = new Vector2(i / (float)PolaroidColors.Length, 0f);
            srt.anchorMax = new Vector2((i + 1) / (float)PolaroidColors.Length, 1f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            var img = stripe.GetComponent<Image>();
            img.color = PolaroidColors[i];
            img.raycastTarget = false;
        }
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
