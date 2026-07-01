#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Configuración CORRECTA del input de UI para la recreativa, centralizada (una sola fuente de verdad
// que reusan el resto de builders). Filosofía: UN EventSystem por escena (NO persistente), con
// InputSystemUIInputModule cableado al mapa "UI" de ArcadeControls (Navigate = WASD/flechas/joystick,
// Submit = J/K/L/Enter/Espacio, Cancel = Esc, Point/Click = ratón). Nada de DontDestroyOnLoad ni de
// clonar assets: cada EventSystem nace y muere con su escena, así no hay duplicados ni estado colgado.
//
// Uso: abre la escena y ejecuta los comandos del menú SpaceSpaceSpace/UI. Luego Ctrl+S.
public static class UiInputSetup
{
    public const string ActionsAssetPath = "Assets/_Project/Input/ArcadeControls.inputactions";

    // Colores del foco: highlight/selección claramente visibles (los del menú eran ~blancos = invisibles).
    private static readonly Color NormalColor = new Color(1f, 1f, 1f, 1f);
    private static readonly Color FocusColor = new Color(1f, 0.85f, 0.30f, 1f);       // ámbar (foco)
    private static readonly Color PressedColor = new Color(0.85f, 0.60f, 0.15f, 1f);
    private static readonly Color DisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    // ===================== Comandos de menú =====================

    [MenuItem("SpaceSpaceSpace/UI/Configure EventSystem (current scene)")]
    public static void ConfigureCurrentScene()
    {
        var es = EnsureSingleEventSystem();
        ConfigureModule(es);
        EditorSceneManager.MarkSceneDirty(es.gameObject.scene);
        Debug.Log("[UiInput] EventSystem configurado: InputSystemUIInputModule + mapa 'UI' de ArcadeControls " +
            "(Navigate=WASD/flechas, Submit=J/K/L/Enter/Espacio). Uno por escena, no persistente. Guarda (Ctrl+S).");
    }

    [MenuItem("SpaceSpaceSpace/UI/Setup Menu Navigation")]
    public static void SetupMenuNavigation()
    {
        var buttons = Object.FindObjectsOfType<Button>(true);
        if (buttons.Length == 0)
        {
            Debug.LogError("[UiInput] No hay Buttons en la escena. Abre la escena Menu y reintenta.");
            return;
        }

        // Orden vertical: de arriba (mayor Y) a abajo.
        System.Array.Sort(buttons, (a, b) => b.transform.position.y.CompareTo(a.transform.position.y));

        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = buttons[i];

            // Highlight/selección visibles.
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.normalColor = NormalColor;
            cb.highlightedColor = FocusColor;
            cb.selectedColor = FocusColor;
            cb.pressedColor = PressedColor;
            cb.disabledColor = DisabledColor;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            // Navegación vertical explícita (envuelve arriba/abajo).
            var nav = new Navigation { mode = Navigation.Mode.Explicit };
            nav.selectOnUp = buttons[(i - 1 + buttons.Length) % buttons.Length];
            nav.selectOnDown = buttons[(i + 1) % buttons.Length];
            btn.navigation = nav;

            EditorUtility.SetDirty(btn);
        }

        // UISelectionKeeper en el Canvas, con el primer botón como selección por defecto.
        Button first = buttons[0];
        Canvas canvas = first.GetComponentInParent<Canvas>();
        GameObject host = canvas != null ? canvas.gameObject : first.gameObject;
        var keeper = host.GetComponent<UISelectionKeeper>();
        if (keeper == null) keeper = host.AddComponent<UISelectionKeeper>();
        SetRef(keeper, "firstSelected", first);

        // Y asegura el EventSystem correcto de paso.
        ConfigureModule(EnsureSingleEventSystem());

        EditorSceneManager.MarkSceneDirty(first.gameObject.scene);
        Debug.Log($"[UiInput] Navegación de menú lista: {buttons.Length} botones con highlight ámbar, " +
            "navegación vertical y selección por defecto ('" + first.name + "'). Guarda (Ctrl+S).");
    }

    // ===================== API compartida (la usan los builders) =====================

    // Asegura EXACTAMENTE un EventSystem en la escena: conserva el primero, borra los demás y limpia
    // scripts "Missing" (restos del antiguo PersistentEventSystem ya borrado).
    public static EventSystem EnsureSingleEventSystem()
    {
        var systems = Object.FindObjectsOfType<EventSystem>();
        EventSystem es = systems.Length > 0 ? systems[0] : null;
        for (int i = 1; i < systems.Length; i++)
            if (systems[i] != null) Object.DestroyImmediate(systems[i].gameObject);

        if (es == null)
            es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(es.gameObject);
        return es;
    }

    // Pone/actualiza el InputSystemUIInputModule cableado al mapa "UI" y elimina el módulo legacy.
    public static void ConfigureModule(EventSystem es)
    {
        var legacy = es.GetComponent<StandaloneInputModule>();
        if (legacy != null) Object.DestroyImmediate(legacy);

        var module = es.GetComponent<InputSystemUIInputModule>();
        if (module == null) module = es.gameObject.AddComponent<InputSystemUIInputModule>();

        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsAssetPath);
        if (actions == null)
        {
            Debug.LogWarning($"[UiInput] No se encontró '{ActionsAssetPath}'. El módulo quedó sin acciones: " +
                "cablea el mapa 'UI' a mano.");
            return;
        }

        module.actionsAsset = actions;
        module.move = FindActionReference(actions, "UI", "Navigate");
        module.submit = FindActionReference(actions, "UI", "Submit");
        module.cancel = FindActionReference(actions, "UI", "Cancel");
        module.point = FindActionReference(actions, "UI", "Point");
        module.leftClick = FindActionReference(actions, "UI", "Click");
        EditorUtility.SetDirty(module);
    }

    // Sub-asset InputActionReference (uno por acción, los genera el import del .inputactions) que es lo
    // único que el módulo puede serializar. Null (con aviso) si no existe.
    public static InputActionReference FindActionReference(InputActionAsset asset, string map, string action)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (obj is InputActionReference r && r.action != null && r.action.actionMap != null &&
                r.action.actionMap.name == map && r.action.name == action)
                return r;
        }
        Debug.LogWarning($"[UiInput] No hay InputActionReference para '{map}/{action}'. Reimporta " +
            "ArcadeControls.inputactions (clic derecho > Reimport) y reintenta.");
        return null;
    }

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[UiInput] Campo '{field}' no encontrado en {target.GetType().Name}."); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }
}
#endif
