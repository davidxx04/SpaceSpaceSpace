#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Herramienta de editor (carpeta "Editor": NO entra en la build) que monta y CABLEA la pantalla de
// introducción de nickname de un clic, mismo patrón que EndScreenBuilder/LeaderboardBuilder:
// idempotente, auto-saneadora y con refs por SerializedObject.
//
// Trabaja sobre la escena Game: cuelga Panel_NicknameEntry del CanvasPopups (junto al EndScreen),
// monta el teclado QWERTY en pantalla (DEL arriba-izquierda, ENTER abajo-derecha) como Buttons de
// uGUI con navegación EXPLÍCITA en rejilla — así la palanca (UI/Navigate = WASD) lo recorre y
// J/K/L (UI/Submit) pulsan la tecla enfocada, sin código de input nuevo. Cablea además la ref
// 'nicknameScreen' del EndScreen para el flujo "press any button" -> nickname si el score califica.
//
// Uso: menú  SpaceSpaceSpace/UI/Build Nickname Entry  (con la escena Game abierta, después de
// Build End Screen). Luego Ctrl+S.
public static class NicknameEntryBuilder
{
    private const string FontDir = "Assets/_Project/Art/Fonts";

    // --- Layout del teclado ---
    private const float KeySize = 48f;        // teclas cuadradas
    private const float KeyGap = 8f;
    private const float DelWidth = 72f;       // DEL y ENTER más anchas (texto)
    private const float EnterWidth = 96f;
    private const float RowStartY = 84f;      // fila superior, relativa al centro del contenedor
    private const float RowStep = KeySize + KeyGap;

    // Distribución QWERTY (decisión del usuario): DEL en la esquina superior izquierda y ENTER en
    // la inferior derecha, como un teclado de móvil.
    private static readonly string[][] Rows =
    {
        new[] { "DEL", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" },
        new[] { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" },
        new[] { "A", "S", "D", "F", "G", "H", "J", "K", "L" },
        new[] { "Z", "X", "C", "V", "B", "N", "M", "ENTER" },
    };

    // --- Paleta (foco ámbar, como el resto de la UI de cabina) ---
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.82f);
    private static readonly Color TitleColor = new Color(1f, 0.85f, 0.30f, 1f);
    private static readonly Color RankColor = new Color(0.90f, 0.93f, 1f, 1f);
    private static readonly Color PromptColor = new Color(0.70f, 0.74f, 0.82f, 1f);
    private static readonly Color NameColor = new Color(1f, 1f, 1f, 1f);

    // Teclas claras con letra oscura (legible tanto en reposo como bajo el foco ámbar).
    private static readonly Color KeyNormal = new Color(0.72f, 0.75f, 0.82f, 1f);
    private static readonly Color KeyFocus = new Color(1f, 0.85f, 0.30f, 1f);
    private static readonly Color KeyPressed = new Color(0.85f, 0.60f, 0.15f, 1f);
    private static readonly Color KeyDisabled = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    private static readonly Color KeyLabel = new Color(0.08f, 0.09f, 0.12f, 1f);

    [MenuItem("SpaceSpaceSpace/UI/Build Nickname Entry")]
    public static void Build()
    {
        // La pantalla vive junto al EndScreen (mismo CanvasPopups) y es él quien la enciende.
        var endScreen = Object.FindObjectOfType<EndScreen>();
        if (endScreen == null)
        {
            Debug.LogError("[Nickname] No hay EndScreen en la escena. Abre la escena Game (y ejecuta antes " +
                "'SpaceSpaceSpace/UI/Build End Screen').");
            return;
        }

        Canvas canvas = FindPopupsCanvas();
        TMP_FontAsset font = FindPixelFont();

        // Panel a pantalla completa (visible en edición para maquetar; oculto en Play por Awake).
        GameObject panel = EnsurePanel(canvas.transform);

        var dim = EnsureImage(panel.transform, "Dim", DimColor);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;
        dim.transform.SetSiblingIndex(0);

        TMP_Text title = EnsureText(panel.transform, "Title", "CONGRATULATIONS!", font, 44f, TitleColor, new Vector2(0f, 200f), new Vector2(900f, 60f));
        TMP_Text rank = EnsureText(panel.transform, "RankText", "YOU REACHED TOP 1!", font, 26f, RankColor, new Vector2(0f, 148f), new Vector2(900f, 40f));
        EnsureText(panel.transform, "Prompt", "INTRODUCE YOUR NICKNAME", font, 20f, PromptColor, new Vector2(0f, 105f), new Vector2(900f, 32f));
        TMP_Text nameField = EnsureText(panel.transform, "NameField", "_", font, 34f, NameColor, new Vector2(0f, 60f), new Vector2(600f, 50f));

        // Componente en la RAÍZ del canvas (siempre activo, como EndScreen) para poder cablear ya
        // los onClick de las teclas.
        var screen = Ensure<NicknameEntryScreen>(canvas.gameObject);

        // Teclado: se destruye y reconstruye entero cada build (autoritativo, sin dedupes).
        Button firstKey = BuildKeyboard(panel.transform, font, screen);

        CleanupChildren(panel.transform);

        SetRef(screen, "panelGroup", panel.GetComponent<CanvasGroup>());
        SetRef(screen, "rankText", rank);
        SetRef(screen, "nameText", nameField);

        // Flujo: EndScreen -> nickname si el score califica.
        SetRef(endScreen, "nicknameScreen", screen);

        // Foco de palanca: mientras el panel no es interactable el keeper no selecciona nada; al
        // mostrarse fija el foco en la primera tecla ('Q').
        var keeper = Ensure<UISelectionKeeper>(panel);
        SetRef(keeper, "firstSelected", firstKey);

        // EventSystem del cabinet listo (fuente única de verdad: UiInputSetup).
        UiInputSetup.ConfigureModule(UiInputSetup.EnsureSingleEventSystem());

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Debug.Log("[Nickname] Pantalla de nickname montada y cableada en CanvasPopups (EndScreen -> nickname " +
            "si el score entra en el top 100). El panel se ve en edición y se oculta solo en Play. Guarda (Ctrl+S).");
    }

    // --- Teclado ---

    private static Button BuildKeyboard(Transform panel, TMP_FontAsset font, NicknameEntryScreen screen)
    {
        Transform old = panel.Find("Keyboard");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var go = new GameObject("Keyboard", typeof(RectTransform));
        go.transform.SetParent(panel, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 260f);
        rt.anchoredPosition = new Vector2(0f, -95f);

        // Construye fila a fila guardando cada botón y su X central (para la navegación vertical
        // por vecino más cercano: las filas QWERTY son desiguales).
        var rowButtons = new List<Button[]>();
        var rowCenters = new List<float[]>();

        for (int r = 0; r < Rows.Length; r++)
        {
            string[] defs = Rows[r];
            var buttons = new Button[defs.Length];
            var centers = new float[defs.Length];

            float total = 0f;
            for (int i = 0; i < defs.Length; i++) total += KeyWidth(defs[i]) + (i > 0 ? KeyGap : 0f);

            float x = -total * 0.5f;
            float y = RowStartY - r * RowStep;
            for (int i = 0; i < defs.Length; i++)
            {
                float w = KeyWidth(defs[i]);
                float cx = x + w * 0.5f;
                buttons[i] = BuildKey(go.transform, defs[i], font, new Vector2(cx, y), new Vector2(w, KeySize), screen);
                centers[i] = cx;
                x += w + KeyGap;
            }

            rowButtons.Add(buttons);
            rowCenters.Add(centers);
        }

        WireNavigation(rowButtons, rowCenters);

        // Primera tecla a enfocar: 'Q' (inicio de la primera fila de letras).
        return rowButtons[1][0];
    }

    private static float KeyWidth(string def) =>
        def == "DEL" ? DelWidth : def == "ENTER" ? EnterWidth : KeySize;

    private static Button BuildKey(Transform keyboard, string def, TMP_FontAsset font,
                                   Vector2 center, Vector2 size, NicknameEntryScreen screen)
    {
        var go = new GameObject("Key_" + def, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(keyboard, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = center;

        // El Image se deja blanco: el color visible lo pone el tinte del Button por estado.
        var img = go.GetComponent<Image>();
        img.color = Color.white;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors;
        cb.normalColor = KeyNormal;
        cb.highlightedColor = KeyFocus;
        cb.selectedColor = KeyFocus;
        cb.pressedColor = KeyPressed;
        cb.disabledColor = KeyDisabled;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;

        // Label centrado (más pequeño en DEL/ENTER para que quepan).
        var lgo = new GameObject("Label", typeof(RectTransform));
        lgo.transform.SetParent(go.transform, false);
        var tmp = lgo.AddComponent<TextMeshProUGUI>();
        tmp.text = def;
        tmp.fontSize = (def == "DEL" || def == "ENTER") ? 16f : 24f;
        tmp.color = KeyLabel;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;
        var lrt = tmp.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

        // onClick persistente según la tecla (los botones son nuevos cada build: sin dedupe).
        switch (def)
        {
            case "DEL":
                UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(screen.DeleteChar));
                break;
            case "ENTER":
                UnityEventTools.AddPersistentListener(btn.onClick, new UnityAction(screen.Confirm));
                break;
            default:
                UnityEventTools.AddStringPersistentListener(btn.onClick, new UnityAction<string>(screen.AppendChar), def);
                break;
        }

        return btn;
    }

    // Navegación explícita de rejilla: izquierda/derecha dentro de la fila (con wrap), arriba/abajo
    // a la tecla de la fila adyacente más cercana en X (sin wrap vertical).
    private static void WireNavigation(List<Button[]> rows, List<float[]> centers)
    {
        for (int r = 0; r < rows.Count; r++)
        {
            Button[] row = rows[r];
            for (int i = 0; i < row.Length; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                nav.selectOnLeft = row[(i - 1 + row.Length) % row.Length];
                nav.selectOnRight = row[(i + 1) % row.Length];
                nav.selectOnUp = r > 0 ? NearestByX(rows[r - 1], centers[r - 1], centers[r][i]) : null;
                nav.selectOnDown = r < rows.Count - 1 ? NearestByX(rows[r + 1], centers[r + 1], centers[r][i]) : null;
                row[i].navigation = nav;
                EditorUtility.SetDirty(row[i]);
            }
        }
    }

    private static Button NearestByX(Button[] row, float[] centers, float x)
    {
        int best = 0;
        float bestDist = Mathf.Abs(centers[0] - x);
        for (int i = 1; i < row.Length; i++)
        {
            float d = Mathf.Abs(centers[i] - x);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return row[best];
    }

    // --- Canvas / panel (mismos helpers que EndScreenBuilder) ---

    private static Canvas FindPopupsCanvas()
    {
        var go = GameObject.Find("CanvasPopups");
        if (go == null)
        {
            go = new GameObject("CanvasPopups", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 100;
        }

        if (go.GetComponent<Canvas>() == null) go.AddComponent<Canvas>();
        if (go.GetComponent<CanvasScaler>() == null) go.AddComponent<CanvasScaler>();
        if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();
        return go.GetComponent<Canvas>();
    }

    private static GameObject EnsurePanel(Transform canvas)
    {
        Transform t = canvas.Find("Panel_NicknameEntry");
        GameObject go = t != null ? t.gameObject : new GameObject("Panel_NicknameEntry", typeof(RectTransform));
        go.name = "Panel_NicknameEntry";
        if (go.transform.parent != canvas) go.transform.SetParent(canvas, false);
        if (go.GetComponent<RectTransform>() == null) go.AddComponent<RectTransform>();
        Stretch(go.GetComponent<RectTransform>());
        go.transform.SetAsLastSibling();   // por encima del Panel_EndScreen en orden de dibujado

        var layout = go.GetComponent<LayoutGroup>(); if (layout != null) Object.DestroyImmediate(layout);
        var fitter = go.GetComponent<ContentSizeFitter>(); if (fitter != null) Object.DestroyImmediate(fitter);

        // Visible en EDICIÓN para maquetar; NicknameEntryScreen lo oculta en Play (Awake).
        var group = Ensure<CanvasGroup>(go);
        group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true;
        return go;
    }

    private static readonly string[] PanelChildWhitelist = { "Dim", "Title", "RankText", "Prompt", "NameField", "Keyboard" };

    private static void CleanupChildren(Transform panel)
    {
        for (int i = panel.childCount - 1; i >= 0; i--)
        {
            string n = panel.GetChild(i).name;
            if (System.Array.IndexOf(PanelChildWhitelist, n) < 0)
                Object.DestroyImmediate(panel.GetChild(i).gameObject);
        }
    }

    // --- Elementos / helpers (mismo estilo que EndScreenBuilder) ---

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
        if (prop == null) { Debug.LogWarning($"[Nickname] Campo '{field}' no encontrado en {target.GetType().Name}."); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
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
