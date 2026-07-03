#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Herramienta de editor (carpeta "Editor": NO entra en la build) que viste el MENÚ de un clic:
// monta el fondo térmico (shader ThermalFlow vía SpaceBackground, patrón BackgroundBuilder) y
// retoca la tipografía del título (degradado cromo 80s cian->magenta + espaciado) y de los botones.
// Idempotente: re-ejecutable sin duplicar nada; el material queda como asset tuneable.
//
// Uso: menú  SpaceSpaceSpace/UI/Build Menu Look  (con la escena Menu abierta). Luego Ctrl+S.
public static class MenuLookBuilder
{
    private const string ShaderName = "SpaceSpaceSpace/ThermalFlow";
    private const string MaterialPath = "Assets/_Project/Art/MenuThermal.mat";
    private const int BackgroundSortingOrder = -100;

    // Cromo ochentero del título: cian arriba -> magenta abajo (sobre fondo térmico oscuro).
    private static readonly Color TitleTop = new Color(0.65f, 0.95f, 1f, 1f);
    private static readonly Color TitleBottom = new Color(1f, 0.35f, 0.75f, 1f);
    private static readonly Color CameraClear = new Color(0.008f, 0.010f, 0.022f, 1f);

    [MenuItem("SpaceSpaceSpace/UI/Build Menu Look")]
    public static void Build()
    {
        // Marcador de escena correcta: el menú es donde vive Btn_Start.
        Button startBtn = FindButton("Btn_Start");
        if (startBtn == null)
        {
            Debug.LogError("[MenuLook] No encuentro 'Btn_Start'. Abre la escena Menu y reintenta.");
            return;
        }

        Camera cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
        if (cam == null)
        {
            Debug.LogError("[MenuLook] No hay cámara en la escena Menu.");
            return;
        }

        // --- Fondo térmico (SpaceBackground reutilizado con el material nuevo) ---
        Material mat = EnsureMaterial();
        if (mat == null)
        {
            Debug.LogError($"[MenuLook] Shader '{ShaderName}' no encontrado (¿compila?). Aborto.");
            return;
        }

        GameObject go = FindOrCreate("MenuBackground", cam.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        // Cablear ANTES de que el [ExecuteAlways] monte su quad (evita el material fallback).
        go.SetActive(false);
        var bg = Ensure<SpaceBackground>(go);
        SetRef(bg, "material", mat);
        SetInt(bg, "sortingOrder", BackgroundSortingOrder);
        go.SetActive(true);

        // La cámara despeja al color del cielo por si el quad no cubre algún borde.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = CameraClear;
        EditorUtility.SetDirty(cam);

        // --- Título: degradado vertical cian->magenta + espaciado (look cromo synthwave) ---
        var title = FindText("Title");
        if (title != null)
        {
            title.color = Color.white;   // blanco: el degradado de vértice pone el color real
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(TitleTop, TitleTop, TitleBottom, TitleBottom);
            title.characterSpacing = 6f;
            EditorUtility.SetDirty(title);
        }
        else Debug.LogWarning("[MenuLook] No encuentro el texto 'Title' (¿se renombró?). Título sin retocar.");

        // --- Botones: labels con algo más de aire (el foco ámbar ya lo pone UiInputSetup) ---
        StyleButtonLabel(startBtn);
        StyleButtonLabel(FindButton("Btn_Leaderboard"));

        EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Debug.Log("[MenuLook] Menú vestido: fondo térmico (tunea MenuThermal.mat: colores de la rampa, " +
            "velocidades, manchas/cruces, scanlines) + título degradado + botones. Guarda (Ctrl+S).");
    }

    private static void StyleButtonLabel(Button btn)
    {
        if (btn == null) return;
        var label = btn.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        label.characterSpacing = 4f;
        EditorUtility.SetDirty(label);
    }

    // Crea/carga el material desde el shader (patrón EnsureMaterial de BackgroundBuilder).
    private static Material EnsureMaterial()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null) return null;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "MenuThermal" };
            AssetDatabase.CreateAsset(mat, MaterialPath);
            AssetDatabase.SaveAssets();
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    // --- Helpers (mismo estilo que el resto de builders) ---

    private static Button FindButton(string name)
    {
        foreach (var b in Object.FindObjectsOfType<Button>(true))
            if (b.name == name) return b;
        return null;
    }

    private static TMP_Text FindText(string goName)
    {
        foreach (var t in Object.FindObjectsOfType<TMP_Text>(true))
            if (t.gameObject.name == goName) return t;
        return null;
    }

    private static GameObject FindOrCreate(string name, Transform parent)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : GameObject.Find(name);
        if (go == null) go = new GameObject(name);
        if (go.transform.parent != parent) go.transform.SetParent(parent, false);
        return go;
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[MenuLook] Campo '{field}' no encontrado en {target.GetType().Name}."); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetInt(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[MenuLook] Campo '{field}' no encontrado en {target.GetType().Name}."); return; }
        p.intValue = value;
        so.ApplyModifiedProperties();
    }
}
#endif
