#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Herramienta de editor (carpeta "Editor": NO entra en la build) que monta y CABLEA la pantalla de fin
// de partida de un clic, siguiendo el patrón de HudBuilder: idempotente, auto-saneador y con refs
// asignadas por SerializedObject (sin pelearse con RectTransforms ni arrastrar nada a mano).
//
// Reutiliza el CanvasPopups y el placeholder Panel_GameOver que ya existen en la escena Game; pone el
// componente EndScreen en la RAÍZ del canvas (siempre activo) y cuelga el panel (oculto en Play) como
// hijo. Además deja el EventSystem de la escena listo para el cabinet vía UiInputSetup (fuente única).
//
// Uso: menú  SpaceSpaceSpace/UI/Build End Screen  (con la escena Game abierta). Luego Ctrl+S.
public static class EndScreenBuilder
{
    private const string FontDir = "Assets/_Project/Art/Fonts";

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.78f);          // oscurecedor del fondo
    private static readonly Color TitleColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color BodyColor = new Color(0.85f, 0.88f, 0.95f, 1f);
    private static readonly Color PromptColor = new Color(0.70f, 0.74f, 0.82f, 1f); // "press any button"

    [MenuItem("SpaceSpaceSpace/UI/Build End Screen")]
    public static void Build()
    {
        var match = Object.FindObjectOfType<MatchController>();
        if (match == null)
        {
            Debug.LogError("[EndScreen] No hay MatchController en la escena. Abre la escena Game.");
            return;
        }

        Canvas canvas = FindPopupsCanvas();
        TMP_FontAsset font = FindPixelFont();

        // Panel a pantalla completa (reutiliza Panel_GameOver / Panel_EndScreen si existe).
        GameObject panel = EnsurePanel(canvas.transform);

        // Fondo oscurecedor (bloquea clics al juego de detrás).
        var dim = EnsureImage(panel.transform, "Dim", DimColor);
        Stretch(dim.rectTransform);
        dim.transform.SetSiblingIndex(0);   // detrás del resto
        dim.raycastTarget = true;

        // Textos centrados (anclados al centro; posición Y relativa). Sin subtítulo: pantalla más limpia.
        TMP_Text title = EnsureText(panel.transform, "Title", "VICTORY", font, 64f, TitleColor, new Vector2(0f, 90f), new Vector2(900f, 90f));
        TMP_Text score = EnsureText(panel.transform, "Score", "SCORE  0", font, 36f, BodyColor, new Vector2(0f, 0f), new Vector2(900f, 60f));

        // "Pulsa cualquier botón": NO es un botón, es texto. EndScreen escucha cualquier tecla y vuelve al menú.
        EnsureText(panel.transform, "Prompt", "PRESS ANY BUTTON", font, 24f, PromptColor, new Vector2(0f, -110f), new Vector2(900f, 40f));

        // Borra restos del placeholder (textos sueltos / botón viejo) fuera de la whitelist.
        CleanupChildren(panel.transform);

        // Componente EndScreen en la RAÍZ del canvas (siempre activo) + cableado de todas las refs.
        var endScreen = Ensure<EndScreen>(canvas.gameObject);
        SetRef(endScreen, "match", match);
        SetRef(endScreen, "panelRoot", panel);
        SetRef(endScreen, "panelGroup", panel.GetComponent<CanvasGroup>());
        SetRef(endScreen, "title", title);
        SetRef(endScreen, "scoreText", score);
        SetRef(endScreen, "subtitle", null);   // ya no hay subtítulo: limpia la ref vieja (evita "Missing")

        // Deja el EventSystem de la escena listo para el cabinet (fuente única de verdad: UiInputSetup).
        UiInputSetup.ConfigureModule(UiInputSetup.EnsureSingleEventSystem());

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[EndScreen] Pantalla de fin montada y cableada en CanvasPopups. Ajusta el layout si " +
            "quieres y guarda (Ctrl+S). El panel se oculta solo en Play hasta el final de la partida.");
    }

    // --- Canvas / panel ---

    private static Canvas FindPopupsCanvas()
    {
        var go = GameObject.Find("CanvasPopups");
        if (go == null)
        {
            go = new GameObject("CanvasPopups", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 100;   // por encima del HUD
        }

        // Asegura los componentes necesarios para clics/escalado aunque el canvas ya existiera.
        if (go.GetComponent<Canvas>() == null) go.AddComponent<Canvas>();
        if (go.GetComponent<CanvasScaler>() == null) go.AddComponent<CanvasScaler>();
        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        return go.GetComponent<Canvas>();
    }

    private static GameObject EnsurePanel(Transform canvas)
    {
        // Reutiliza Panel_EndScreen o el placeholder Panel_GameOver; si no, lo crea.
        Transform t = canvas.Find("Panel_EndScreen");
        if (t == null) t = canvas.Find("Panel_GameOver");

        GameObject go = t != null ? t.gameObject : new GameObject("Panel_EndScreen", typeof(RectTransform));
        go.name = "Panel_EndScreen";
        if (go.transform.parent != canvas) go.transform.SetParent(canvas, false);
        if (go.GetComponent<RectTransform>() == null) go.AddComponent<RectTransform>();
        Stretch(go.GetComponent<RectTransform>());

        // El placeholder Panel_GameOver traía layout automático (VerticalLayoutGroup/ContentSizeFitter)
        // que pisaría nuestras posiciones explícitas: fuera, montamos con RectTransforms como HudBuilder.
        var layout = go.GetComponent<LayoutGroup>();
        if (layout != null) Object.DestroyImmediate(layout);
        var fitter = go.GetComponent<ContentSizeFitter>();
        if (fitter != null) Object.DestroyImmediate(fitter);

        // Visibilidad por CanvasGroup: EndScreen lo pone a alpha 0 en Play y a 1 al terminar. En
        // EDICIÓN lo dejamos visible (alpha 1) para poder maquetar. (El placeholder venía con alpha 0,
        // que era justo por lo que el popup no se veía.)
        var group = Ensure<CanvasGroup>(go);
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;

        return go;
    }

    // Hijos que el builder posee en el panel; cualquier otro (restos del placeholder) se borra.
    private static readonly string[] PanelChildWhitelist = { "Dim", "Title", "Score", "Prompt" };

    private static void CleanupChildren(Transform panel)
    {
        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            string n = panel.GetChild(i).name;
            if (System.Array.IndexOf(PanelChildWhitelist, n) < 0)
                Object.DestroyImmediate(panel.GetChild(i).gameObject);
        }
    }

    // --- Elementos ---

    private static Image EnsureImage(Transform parent, string name, Color color)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        if (t == null) go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static TMP_Text EnsureText(Transform parent, string name, string text, TMP_FontAsset font,
                                       float size, Color color, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name, typeof(RectTransform));
        if (t == null) go.transform.SetParent(parent, false);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

        var rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPos;
        return tmp;
    }

    // --- Helpers (mismo estilo que HudBuilder) ---

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void SetRef(Object target, string field, Object value)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogWarning($"[EndScreen] Campo '{field}' no encontrado en {target.GetType().Name}."); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    // Busca la primera fuente pixel (TMP_FontAsset) en Art/Fonts; si no hay, usa la por defecto de TMP.
    private static TMP_FontAsset FindPixelFont()
    {
        if (!AssetDatabase.IsValidFolder(FontDir)) return null;
        var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontDir });
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
#endif
