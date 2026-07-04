using UnityEngine;
using UnityEngine.UI;

// Hoja "halftone de ruido" para paneles de popup congelados (EndScreen y NicknameEntryScreen),
// construida POR CÓDIGO en runtime (sin builder, sin tocar la jerarquía de la escena): el dueño
// llama a Create en Awake y en cada Show le pasa el material del look (EndVictory/EndGameOver/
// EndSurvived/EndNickname.mat) vía Apply.
//
// Dos diferencias con LeaderboardSheetFx:
//  - El material NO sale de Shader.Find: se COPIA del .mat asset elegido. Así empujar _Size y
//    _UnscaledTime jamás ensucia el asset en el editor, y editar el .mat en Play se refleja en el
//    siguiente Show (la copia es por-Show). El asset referenciado desde la escena también evita
//    el stripping del shader en builds (no necesita Always Included).
//  - El end screen vive con timeScale = 0 y _Time del shader se congela, así que aquí se empuja
//    _UnscaledTime cada frame mientras el panel está visible (mismo patrón que NeonButtonFx).
public class EndScreenSheetFx : MonoBehaviour
{
    private static readonly int SizeId = Shader.PropertyToID("_Size");
    private static readonly int UnscaledTimeId = Shader.PropertyToID("_UnscaledTime");

    private Image image;
    private Material mat;
    private CanvasGroup group;

    // Crea (idempotente) el fondo "HalftoneBg" bajo el panel: full-stretch, tras el Dim (índice 1)
    // y debajo de Title/Score/Prompt. Apagado hasta el primer Apply (coste cero durante la partida).
    public static EndScreenSheetFx Create(RectTransform panel)
    {
        if (panel == null) return null;

        Transform prev = panel.Find("HalftoneBg");
        if (prev != null) return prev.GetComponent<EndScreenSheetFx>();

        var go = new GameObject("HalftoneBg", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(panel, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.transform.SetSiblingIndex(1);   // tras Dim (0), bajo Title/Score/Prompt

        var img = go.GetComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = false;
        img.enabled = false;               // hasta el primer Apply

        return go.AddComponent<EndScreenSheetFx>();
    }

    // Activa la hoja con el look del desenlace. Copia fresca por Show: sin ensuciar el asset y
    // recogiendo cualquier ajuste hecho al .mat entre pantallas.
    public void Apply(Material source)
    {
        if (source == null || image == null) return;

        if (mat != null) Destroy(mat);
        mat = new Material(source) { name = source.name + " (runtime)" };
        image.material = mat;
        image.enabled = true;

        PushSize();
        mat.SetFloat(UnscaledTimeId, Time.unscaledTime);   // primer frame ya fresco
    }

    private void Awake()
    {
        image = GetComponent<Image>();
        group = GetComponentInParent<CanvasGroup>();
    }

    // En el Awake de carga el canvas aún no ha maquetado (rect puede ser 0): reintenta.
    private void Start() => PushSize();

    private void OnRectTransformDimensionsChange() => PushSize();

    private void Update()
    {
        if (mat == null) return;                          // sin Show todavía: gratis en partida
        if (group != null && group.alpha <= 0f) return;   // panel oculto
        mat.SetFloat(UnscaledTimeId, Time.unscaledTime);
    }

    private void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }

    // El shader trabaja en px de canvas: necesita el tamaño real del rect (celdas cuadradas).
    private void PushSize()
    {
        if (mat == null) return;
        Vector2 size = ((RectTransform)transform).rect.size;
        if (size.x < 1f || size.y < 1f) return;   // layout aún no resuelto
        mat.SetVector(SizeId, size);
    }
}
