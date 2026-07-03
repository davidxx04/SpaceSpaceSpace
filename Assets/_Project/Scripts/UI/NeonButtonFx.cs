using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Estados del botón NEÓN (shader NeonPlate). La UI de Unity no soporta MaterialPropertyBlocks,
// así que este componente INSTANCIA los materiales de su placa (y overlay de líneas, si hay) en
// Awake y les empuja por frame:
//   _UnscaledTime -> reloj del shader (Time.unscaledTime: el flicker sigue vivo a timeScale = 0,
//                    imprescindible en la pantalla de nickname congelada).
//   _Focus        -> 0..1 suavizado; la SELECCIÓN del EventSystem es el "hover" de la palanca
//                    (UISelectionKeeper garantiza que siempre haya algo seleccionado).
//   _PressTime    -> marca de tiempo al pulsar (destello con decay en el shader).
//   _Size         -> tamaño del RectTransform en px, para el SDF y el grosor real de las líneas.
//
// El Button debe ir con transition = None (el shader pinta los estados); lo dejan así los builders.
[DisallowMultipleComponent]
public class NeonButtonFx : MonoBehaviour, ISelectHandler, IDeselectHandler,
    IPointerEnterHandler, IPointerExitHandler, ISubmitHandler, IPointerClickHandler
{
    [Tooltip("Image de la placa (material NeonPlate).")]
    [SerializeField] private Image plate;
    [Tooltip("Image opcional con las líneas por ENCIMA del texto (material NeonPlate en modo Lines Only).")]
    [SerializeField] private Image linesOverlay;
    [Tooltip("Velocidad del fundido del foco (1/s).")]
    [SerializeField] private float focusLerpSpeed = 14f;

    [Header("Parpadeo de la letra")]
    [Tooltip("Texto (o gráfico) del botón: parpadea su alfa mientras está enfocado, en sync con el borde.")]
    [SerializeField] private Graphic label;
    [SerializeField] private bool flickerLabel = true;
    [Tooltip("Alfa mínimo de la letra en el parpadeo (no baja de aquí: legibilidad).")]
    [SerializeField] private float labelFlickerMin = 0.55f;
    [Tooltip("Frecuencia del parpadeo (Hz). Igualar a Border Flicker Rate del material para que vayan juntos.")]
    [SerializeField] private float labelFlickerRate = 7f;

    private Material plateMat;
    private Material overlayMat;
    private float focus;
    private float focusTarget;
    private Color labelBaseColor = Color.white;

    private static readonly int SizeId = Shader.PropertyToID("_Size");
    private static readonly int FocusId = Shader.PropertyToID("_Focus");
    private static readonly int PressTimeId = Shader.PropertyToID("_PressTime");
    private static readonly int UnscaledTimeId = Shader.PropertyToID("_UnscaledTime");

    private void Awake()
    {
        // Instancias propias: cada botón necesita su foco/tamaño sin pisar el .mat compartido.
        if (plate != null && plate.material != null)
        {
            plateMat = Instantiate(plate.material);
            plate.material = plateMat;
        }
        if (linesOverlay != null && linesOverlay.material != null)
        {
            overlayMat = Instantiate(linesOverlay.material);
            linesOverlay.material = overlayMat;
        }
        if (label != null) labelBaseColor = label.color;
        PushSize();
    }

    private void OnDestroy()
    {
        if (plateMat != null) Destroy(plateMat);
        if (overlayMat != null) Destroy(overlayMat);
    }

    private void OnRectTransformDimensionsChange()
    {
        PushSize();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        focus = Mathf.MoveTowards(focus, focusTarget, focusLerpSpeed * dt);

        float t = Time.unscaledTime;
        if (plateMat != null)
        {
            plateMat.SetFloat(UnscaledTimeId, t);
            plateMat.SetFloat(FocusId, focus);
        }
        if (overlayMat != null) overlayMat.SetFloat(UnscaledTimeId, t);

        if (label != null && flickerLabel)
        {
            // Mismo blink por hash que el borde del shader (misma tasa) -> van a una.
            float blink = Mathf.Lerp(labelFlickerMin, 1f, Hash11(Mathf.Floor(t * labelFlickerRate) * 3.17f + 0.7f));
            float alpha = labelBaseColor.a * Mathf.Lerp(1f, blink, focus);
            var c = labelBaseColor; c.a = alpha;
            label.color = c;
        }
    }

    // Puerto del hash11 del shader NeonPlate (para que el parpadeo de la letra case con el del borde).
    private static float Hash11(float p)
    {
        p = Frac(p * 0.1031f);
        p *= p + 33.33f;
        return Frac(p * (p + p));
    }

    private static float Frac(float x) => x - Mathf.Floor(x);

    // Selección del EventSystem = hover de palanca; punteros solo por comodidad en desarrollo.
    public void OnSelect(BaseEventData _) => focusTarget = 1f;
    public void OnDeselect(BaseEventData _) => focusTarget = 0f;
    public void OnPointerEnter(PointerEventData _) => focusTarget = 1f;
    public void OnPointerExit(PointerEventData _) => focusTarget = EventSystem.current != null &&
        EventSystem.current.currentSelectedGameObject == gameObject ? 1f : 0f;

    public void OnSubmit(BaseEventData _) => Flash();
    public void OnPointerClick(PointerEventData _) => Flash();

    private void Flash()
    {
        if (plateMat != null) plateMat.SetFloat(PressTimeId, Time.unscaledTime);
    }

    private void PushSize()
    {
        if (plateMat == null && overlayMat == null) return;
        Vector2 size = ((RectTransform)transform).rect.size;
        if (plateMat != null) plateMat.SetVector(SizeId, size);
        if (overlayMat != null) overlayMat.SetVector(SizeId, size);
    }
}
