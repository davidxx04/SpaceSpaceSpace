#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Herramienta de editor (carpeta "Editor": NO entra en la build) que monta y CABLEA el popup del
// leaderboard de un clic, mismo patrón que EndScreenBuilder/HudBuilder: idempotente, auto-saneador y
// con las refs asignadas por SerializedObject (sin arrastrar nada a mano).
//
// Trabaja sobre la escena Menu: cuelga Panel_Leaderboard de un canvas PROPIO ("CanvasPopups", como
// en la escena Game) — NO del canvas del menú, cuyo VerticalLayoutGroup capturaría el panel como un
// elemento más de la columna de botones y lo aplastaría a tamaño cero. Genera el prefab de fila
// (Assets/_Project/Prefabs/LeaderboardRow.prefab), pone el componente LeaderboardPopup en la RAÍZ de
// CanvasPopups (siempre activo, como EndScreen) y conecta el OnClick del botón a Show(). El panel se
// ve en EDICIÓN (para maquetar) y el componente lo oculta solo en Play.
//
// Uso: menú  SpaceSpaceSpace/UI/Build Leaderboard  (con la escena Menu abierta). Luego Ctrl+S.
public static class LeaderboardBuilder
{
    private const string FontDir = "Assets/_Project/Art/Fonts";
    private const string PrefabPath = "Assets/_Project/Prefabs/LeaderboardRow.prefab";

    // Layout compartido entre cabecera y filas para que las columnas cuadren (mismos anchos, mismo hueco).
    private const float RankWidth = 90f;
    private const float ScoreWidth = 170f;
    private const float RowHeight = 40f;
    private const float ColSpacing = 12f;
    private const float SideMargin = 48f;
    private const float TopMargin = 40f;
    private const float BottomMargin = 40f;
    private const float HeaderHeight = 50f;
    private const float HeaderGap = 10f;

    private static readonly Color DimColor = new Color(0.03f, 0.04f, 0.07f, 0.93f);   // oscurecedor del menú
    private static readonly Color HeaderColor = new Color(1f, 0.85f, 0.30f, 1f);      // ámbar (mismo foco que el menú)
    private static readonly Color RowColor = new Color(0.90f, 0.93f, 1f, 1f);
    private static readonly Color EmptyColor = new Color(0.70f, 0.74f, 0.82f, 1f);

    [MenuItem("SpaceSpaceSpace/UI/Build Leaderboard")]
    public static void Build()
    {
        // 1) Localiza el botón y su Canvas (fuente robusta: no depende del nombre del Canvas).
        Button leaderboardBtn = FindButton("Btn_Leaderboard");
        if (leaderboardBtn == null)
        {
            Debug.LogError("[Leaderboard] No encuentro 'Btn_Leaderboard' en la escena. Abre la escena Menu y reintenta.");
            return;
        }
        Canvas menuCanvas = leaderboardBtn.GetComponentInParent<Canvas>();
        if (menuCanvas == null) { Debug.LogError("[Leaderboard] 'Btn_Leaderboard' no cuelga de ningún Canvas."); return; }

        var menuGroup = Ensure<CanvasGroup>(menuCanvas.gameObject);   // el popup lo desactiva mientras manda
        TMP_FontAsset font = FindPixelFont();

        // Canvas propio para popups (mismo patrón que la escena Game). Imprescindible: el canvas del
        // menú lleva un VerticalLayoutGroup que trataría el panel como un botón más y lo aplastaría.
        Canvas popups = EnsurePopupsCanvas(menuCanvas);

        // Auto-saneo de builds anteriores: paneles/componentes colgados del canvas equivocado.
        RemoveStrays(popups);

        // 2) Prefab de fila (autoritativo: se regenera cada build).
        LeaderboardRow rowPrefab = BuildRowPrefab(font);

        // 3) Panel a pantalla completa con su CanvasGroup (visible en edición; el componente lo oculta en Play).
        GameObject panel = EnsurePanel(popups.transform);
        var dim = EnsureImage(panel.transform, "Dim", DimColor);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;               // bloquea clics al menú de detrás
        dim.transform.SetSiblingIndex(0);       // fondo

        // 4) Cabecera (3 columnas alineadas a las filas).
        BuildHeader(panel.transform, font);

        // 5) Scroll View: ScrollRect + Viewport(RectMask2D) + Content(VerticalLayoutGroup + ContentSizeFitter).
        ScrollRect scroll = BuildScrollView(panel.transform, out RectTransform content);

        // 6) Estado vacío centrado.
        GameObject empty = BuildEmptyState(panel.transform, font);

        // 7) Borra restos ajenos al layout del builder.
        CleanupChildren(panel.transform);

        // 8) Componente en la RAÍZ de CanvasPopups (siempre activo, como EndScreen) + cableado de refs.
        var popup = Ensure<LeaderboardPopup>(popups.gameObject);
        SetRef(popup, "panelGroup", panel.GetComponent<CanvasGroup>());
        SetRef(popup, "scrollRect", scroll);
        SetRef(popup, "content", content);
        SetRef(popup, "rowPrefab", rowPrefab);
        SetRef(popup, "emptyState", empty);
        SetRef(popup, "menuGroup", menuGroup);
        SetRef(popup, "returnSelection", leaderboardBtn);

        // 9) Cablea Btn_Leaderboard.OnClick -> popup.Show() (idempotente).
        WireButton(leaderboardBtn, popup);

        // 10) EventSystem del cabinet listo (fuente única de verdad: UiInputSetup).
        UiInputSetup.ConfigureModule(UiInputSetup.EnsureSingleEventSystem());

        EditorSceneManager.MarkSceneDirty(menuCanvas.gameObject.scene);
        Debug.Log("[Leaderboard] Popup montado y cableado en '" + popups.name + "' (Btn_Leaderboard -> Show). " +
            "El panel se ve en edición para maquetar y se oculta solo en Play. Guarda (Ctrl+S). " +
            "Para probar el scroll con 100 filas: añade LeaderboardSeeder a un objeto y usa 'Seed test data'.");
    }

    // --- Canvas de popups ---

    // Canvas separado por encima del menú (sortingOrder 100), como el CanvasPopups de la escena Game.
    // Copia el escalado del canvas del menú para que las medidas (márgenes, alturas) signifiquen lo mismo.
    private static Canvas EnsurePopupsCanvas(Canvas menuCanvas)
    {
        var go = GameObject.Find("CanvasPopups");
        if (go == null) go = new GameObject("CanvasPopups", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (go.GetComponent<Canvas>() == null) go.AddComponent<Canvas>();
        if (go.GetComponent<CanvasScaler>() == null) go.AddComponent<CanvasScaler>();
        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();

        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;   // por encima del menú

        var src = menuCanvas.GetComponent<CanvasScaler>();
        var dst = go.GetComponent<CanvasScaler>();
        if (src != null)
        {
            dst.uiScaleMode = src.uiScaleMode;
            dst.referenceResolution = src.referenceResolution;
            dst.screenMatchMode = src.screenMatchMode;
            dst.matchWidthOrHeight = src.matchWidthOrHeight;
            dst.referencePixelsPerUnit = src.referencePixelsPerUnit;
        }
        return c;
    }

    // Borra restos de builds anteriores fuera de CanvasPopups: el Panel_Leaderboard que quedó bajo el
    // canvas del menú (aplastado por su layout group) y cualquier LeaderboardPopup en otro objeto.
    private static void RemoveStrays(Canvas popups)
    {
        foreach (var p in Object.FindObjectsOfType<LeaderboardPopup>(true))
            if (p != null && p.gameObject != popups.gameObject)
                Object.DestroyImmediate(p);

        foreach (var t in Object.FindObjectsOfType<Transform>(true))
            if (t != null && t.name == "Panel_Leaderboard" && t.parent != popups.transform)
                Object.DestroyImmediate(t.gameObject);
    }

    // --- Panel ---

    private static GameObject EnsurePanel(Transform canvas)
    {
        Transform t = canvas.Find("Panel_Leaderboard");
        GameObject go = t != null ? t.gameObject : new GameObject("Panel_Leaderboard", typeof(RectTransform));
        go.name = "Panel_Leaderboard";
        if (go.transform.parent != canvas) go.transform.SetParent(canvas, false);
        if (go.GetComponent<RectTransform>() == null) go.AddComponent<RectTransform>();
        Stretch(go.GetComponent<RectTransform>());
        go.transform.SetAsLastSibling();   // dibujado por encima de los botones del menú

        // Fuera cualquier layout automático que pisaría nuestras posiciones explícitas.
        var layout = go.GetComponent<LayoutGroup>(); if (layout != null) Object.DestroyImmediate(layout);
        var fitter = go.GetComponent<ContentSizeFitter>(); if (fitter != null) Object.DestroyImmediate(fitter);

        // Visibilidad por CanvasGroup. En EDICIÓN visible (alpha 1) para maquetar; el componente lo pone
        // a 0 en Play (Awake -> SetVisible(false)). Si molesta al editar el menú, baja el alpha a mano.
        var group = Ensure<CanvasGroup>(go);
        group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true;
        return go;
    }

    // Hijos que el builder posee en el panel; cualquier otro (restos de placeholders) se borra.
    private static readonly string[] PanelChildWhitelist = { "Dim", "Header", "ScrollView", "EmptyState" };

    private static void CleanupChildren(Transform panel)
    {
        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            string n = panel.GetChild(i).name;
            if (System.Array.IndexOf(PanelChildWhitelist, n) < 0)
                Object.DestroyImmediate(panel.GetChild(i).gameObject);
        }
    }

    // --- Cabecera ---

    private static void BuildHeader(Transform panel, TMP_FontAsset font)
    {
        Transform old = panel.Find("Header");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var go = new GameObject("Header", typeof(RectTransform));
        go.transform.SetParent(panel, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(SideMargin, -(TopMargin + HeaderHeight));
        rt.offsetMax = new Vector2(-SideMargin, -TopMargin);

        AddColumnsLayout(go);
        Column(go.transform, "RankLabel", "#", font, HeaderColor, 26f, TextAlignmentOptions.Center, RankWidth, false);
        Column(go.transform, "NameLabel", "NOMBRE", font, HeaderColor, 26f, TextAlignmentOptions.Left, null, true);
        Column(go.transform, "ScoreLabel", "SCORE", font, HeaderColor, 26f, TextAlignmentOptions.Right, ScoreWidth, false);
    }

    // --- Scroll View ---

    private static ScrollRect BuildScrollView(Transform panel, out RectTransform content)
    {
        Transform old = panel.Find("ScrollView");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect));
        scrollGo.transform.SetParent(panel, false);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.pivot = new Vector2(0.5f, 0.5f);
        srt.offsetMin = new Vector2(SideMargin, BottomMargin);                              // izq / abajo
        srt.offsetMax = new Vector2(-SideMargin, -(TopMargin + HeaderHeight + HeaderGap));  // der / debajo de la cabecera

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;   // rueda de ratón (dev); la palanca la mueve LeaderboardPopup directamente

        // Viewport con RectMask2D: recorta las filas fuera de vista (sin sprite/máscara extra).
        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var vrt = viewportGo.GetComponent<RectTransform>();
        vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one; vrt.pivot = new Vector2(0.5f, 1f);
        vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;

        // Content anclado arriba, crece hacia abajo (setup vertical canónico de Scroll View).
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var crt = contentGo.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0.5f, 1f);
        crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero; crt.sizeDelta = Vector2.zero;

        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2f; vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.childControlWidth = true; vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;

        var sizeFitter = contentGo.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        scroll.viewport = vrt;
        scroll.content = crt;
        content = crt;
        return scroll;
    }

    // --- Estado vacío ---

    private static GameObject BuildEmptyState(Transform panel, TMP_FontAsset font)
    {
        Transform old = panel.Find("EmptyState");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var go = new GameObject("EmptyState", typeof(RectTransform));
        go.transform.SetParent(panel, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "Aún no hay puntuaciones";
        tmp.color = EmptyColor; tmp.fontSize = 30f;
        tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

        var rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 60f); rt.anchoredPosition = Vector2.zero;
        return go;
    }

    // --- Prefab de fila ---

    private static LeaderboardRow BuildRowPrefab(TMP_FontAsset font)
    {
        EnsureFolder("Assets/_Project/Prefabs");

        var go = new GameObject("LeaderboardRow", typeof(RectTransform));
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = RowHeight; le.preferredHeight = RowHeight;
        AddColumnsLayout(go);

        var rank = Column(go.transform, "RankText", "1", font, RowColor, 24f, TextAlignmentOptions.Center, RankWidth, false);
        var nick = Column(go.transform, "NicknameText", "PLAYER", font, RowColor, 24f, TextAlignmentOptions.Left, null, true);
        var score = Column(go.transform, "ScoreText", "0", font, RowColor, 24f, TextAlignmentOptions.Right, ScoreWidth, false);

        var row = go.AddComponent<LeaderboardRow>();
        SetRef(row, "rankText", rank);
        SetRef(row, "nicknameText", nick);
        SetRef(row, "scoreText", score);

        var saved = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        return saved.GetComponent<LeaderboardRow>();
    }

    // --- Columnas (compartido por cabecera y fila para que cuadren) ---

    private static void AddColumnsLayout(GameObject host)
    {
        var hlg = Ensure<HorizontalLayoutGroup>(host);
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.spacing = ColSpacing;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
    }

    private static TMP_Text Column(Transform parent, string name, string text, TMP_FontAsset font, Color color,
                                   float fontSize, TextAlignmentOptions align, float? fixedWidth, bool flexible)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.color = color; tmp.fontSize = fontSize;
        tmp.alignment = align; tmp.raycastTarget = false; tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        if (font != null) tmp.font = font;

        var le = go.AddComponent<LayoutElement>();
        if (fixedWidth.HasValue) { le.preferredWidth = fixedWidth.Value; le.flexibleWidth = 0f; }
        if (flexible) le.flexibleWidth = 1f;
        return tmp;
    }

    // --- Botón ---

    private static void WireButton(Button btn, LeaderboardPopup popup)
    {
        // Quita listeners persistentes previos que apunten a un LeaderboardPopup, o muertos (target
        // destruido por el auto-saneo). Evita duplicar/romper al re-ejecutar el builder.
        for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            Object target = btn.onClick.GetPersistentTarget(i);
            if (target == null || target is LeaderboardPopup)
                UnityEventTools.RemovePersistentListener(btn.onClick, i);
        }

        UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(popup.Show));
        EditorUtility.SetDirty(btn);
    }

    // --- Helpers (mismo estilo que EndScreenBuilder/HudBuilder) ---

    private static Button FindButton(string name)
    {
        foreach (var b in Object.FindObjectsOfType<Button>(true))
            if (b.name == name) return b;
        return null;
    }

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
        if (prop == null) { Debug.LogWarning($"[Leaderboard] Campo '{field}' no encontrado en {target.GetType().Name}."); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static TMP_FontAsset FindPixelFont()
    {
        if (!AssetDatabase.IsValidFolder(FontDir)) return null;
        var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontDir });
        if (guids.Length == 0) return null;
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
#endif
