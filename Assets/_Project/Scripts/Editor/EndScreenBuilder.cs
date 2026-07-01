#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Herramienta de editor (carpeta "Editor": NO entra en la build) que monta y CABLEA la pantalla de fin
// de partida de un clic, siguiendo el patrón de HudBuilder: idempotente, auto-saneador y con refs
// asignadas por SerializedObject (sin pelearse con RectTransforms ni arrastrar nada a mano).
//
// Reutiliza el CanvasPopups y el placeholder Panel_GameOver que ya existen en la escena Game; pone el
// componente EndScreen en la RAÍZ del canvas (siempre activo) y cuelga el panel (oculto en Play) como
// hijo. Además prepara el EventSystem para el cabinet: sustituye el StandaloneInputModule legacy (lee el
// viejo Input Manager, los botones J/K/L no llegan a "Submit") por InputSystemUIInputModule cableado al
// mapa "UI" de ArcadeControls (Submit = J/K/L, Navigate = palanca).
//
// Uso: menú  SpaceSpaceSpace/UI/Build End Screen  (con la escena Game abierta). Luego Ctrl+S.
public static class EndScreenBuilder
{
    private const string FontDir = "Assets/_Project/Art/Fonts";
    private const string ActionsAssetPath = "Assets/_Project/Input/ArcadeControls.inputactions";

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.78f);          // oscurecedor del fondo
    private static readonly Color TitleColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color BodyColor = new Color(0.85f, 0.88f, 0.95f, 1f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.18f, 0.24f, 1f);
    private static readonly Color ButtonTextColor = new Color(0.95f, 0.97f, 1f, 1f);

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

        // Textos centrados (anclados al centro; posición Y relativa).
        TMP_Text title = EnsureText(panel.transform, "Title", "VICTORY", font, 64f, TitleColor, new Vector2(0f, 120f), new Vector2(900f, 90f));
        TMP_Text subtitle = EnsureText(panel.transform, "Subtitle", "", font, 28f, BodyColor, new Vector2(0f, 50f), new Vector2(900f, 50f));
        TMP_Text score = EnsureText(panel.transform, "Score", "SCORE  0", font, 36f, BodyColor, new Vector2(0f, -20f), new Vector2(900f, 60f));

        // Botón único "Volver al menú".
        Button button = EnsureButton(panel.transform, "MenuButton", "VOLVER AL MENÚ", font, new Vector2(0f, -120f), new Vector2(360f, 64f));

        // Borra restos del placeholder (textos sueltos que traía Panel_GameOver) fuera de la whitelist.
        CleanupChildren(panel.transform);

        // Componente EndScreen en la RAÍZ del canvas (siempre activo) + cableado de todas las refs.
        var endScreen = Ensure<EndScreen>(canvas.gameObject);
        SetRef(endScreen, "match", match);
        SetRef(endScreen, "panelRoot", panel);
        SetRef(endScreen, "panelGroup", panel.GetComponent<CanvasGroup>());
        SetRef(endScreen, "title", title);
        SetRef(endScreen, "subtitle", subtitle);
        SetRef(endScreen, "scoreText", score);
        SetRef(endScreen, "menuButton", button);

        // Deja el EventSystem listo para navegar/confirmar con palanca + botones del cabinet.
        EnsureUiInputModule();

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[EndScreen] Pantalla de fin montada y cableada en CanvasPopups. Ajusta el layout si " +
            "quieres y guarda (Ctrl+S). El panel se oculta solo en Play hasta el final de la partida.");
    }

    // --- EventSystem / input (cabinet) ---

    // Deja el EventSystem de la escena listo para el cabinet: cambia el StandaloneInputModule legacy
    // (lee el viejo Input Manager, así que los botones J/K/L NUNCA llegan a "Submit") por
    // InputSystemUIInputModule cableado al mapa "UI" de ArcadeControls (Submit = J/K/L/Enter/Espacio,
    // Navigate = palanca/WASD/flechas, más ratón para probar en el editor). Idempotente: si ya está
    // puesto solo re-cablea las referencias. El módulo habilita esas acciones solo al activarse.
    private static void EnsureUiInputModule()
    {
        var es = Object.FindObjectOfType<EventSystem>();
        if (es == null)
            es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

        // Fuera el módulo legacy: no navega/confirma con el backend del nuevo Input System.
        var legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null) Object.DestroyImmediate(legacy);

        var module = es.GetComponent<InputSystemUIInputModule>();
        if (module == null) module = es.gameObject.AddComponent<InputSystemUIInputModule>();

        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsAssetPath);
        if (actions == null)
        {
            Debug.LogWarning($"[EndScreen] No se encontró el asset de acciones en '{ActionsAssetPath}'. " +
                "El EventSystem quedó con InputSystemUIInputModule pero SIN acciones: cablea el mapa 'UI' a mano.");
            return;
        }

        module.actionsAsset = actions;
        module.move = FindActionReference(actions, "UI", "Navigate");
        module.submit = FindActionReference(actions, "UI", "Submit");
        module.cancel = FindActionReference(actions, "UI", "Cancel");
        module.point = FindActionReference(actions, "UI", "Point");
        module.leftClick = FindActionReference(actions, "UI", "Click");

        EditorUtility.SetDirty(module);
        EditorSceneManager.MarkSceneDirty(es.gameObject.scene);
        Debug.Log("[EndScreen] EventSystem preparado para el cabinet: InputSystemUIInputModule + mapa 'UI' " +
            "(Submit = J/K/L/Enter/Espacio, Navigate = palanca/WASD/flechas).");
    }

    // Devuelve el sub-asset InputActionReference (los genera el import del .inputactions, uno por acción)
    // de una acción concreta; es lo único que el módulo puede serializar. Null si no existe todavía.
    private static InputActionReference FindActionReference(InputActionAsset asset, string map, string action)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (obj is InputActionReference reference && reference.action != null &&
                reference.action.actionMap != null &&
                reference.action.actionMap.name == map && reference.action.name == action)
                return reference;
        }

        Debug.LogWarning($"[EndScreen] No hay InputActionReference para '{map}/{action}'. Reimporta " +
            "ArcadeControls.inputactions (clic derecho > Reimport) y vuelve a ejecutar el builder.");
        return null;
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
    private static readonly string[] PanelChildWhitelist = { "Dim", "Title", "Subtitle", "Score", "MenuButton" };

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

    private static Button EnsureButton(Transform parent, string name, string label, TMP_FontAsset font,
                                       Vector2 anchoredPos, Vector2 size)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        if (t == null) go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = ButtonColor;

        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        // Etiqueta hija (ocupa todo el botón).
        Transform lt = go.transform.Find("Label");
        GameObject lg = lt != null ? lt.gameObject : new GameObject("Label", typeof(RectTransform));
        if (lt == null) lg.transform.SetParent(go.transform, false);
        var tmp = lg.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = lg.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = ButtonTextColor;
        tmp.fontSize = 26f;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        Stretch(tmp.rectTransform);

        return btn;
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
