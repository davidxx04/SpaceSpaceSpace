#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Herramienta de editor (carpeta "Editor": NO entra en la build) que monta y CABLEA el fondo espacial
// de un clic, al estilo de HudBuilder: crea el material tuneable desde el shader y cuelga un
// SpaceBackground de la cámara (para heredar el CameraShake y cubrir siempre la vista). Idempotente.
//
// Uso: menú  SpaceSpaceSpace/Build Background  (con la escena Game abierta). Luego Ctrl+S.
public static class BackgroundBuilder
{
    private const string ShaderName = "SpaceSpaceSpace/SpaceNebula";
    private const string MaterialPath = "Assets/_Project/Art/SpaceNebula.mat";
    private const int BackgroundSortingOrder = -100;

    [MenuItem("SpaceSpaceSpace/Build Background")]
    public static void Build()
    {
        Camera cam = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
        if (cam == null)
        {
            Debug.LogError("[Background] No hay cámara en la escena. Abre la escena Game.");
            return;
        }

        Material mat = EnsureMaterial();
        if (mat == null)
        {
            Debug.LogError($"[Background] Shader '{ShaderName}' no encontrado (¿compila?). Aborto.");
            return;
        }

        // GameObject SpaceBackground colgado de la cámara (hereda CameraShake, siempre cubre la vista).
        GameObject go = FindOrCreate("SpaceBackground", cam.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        // Cablea el material ANTES de que el componente ([ExecuteAlways]) monte su quad, para que no
        // cree un material fallback: añade con el GO inactivo, cablea, y reactiva.
        go.SetActive(false);
        var bg = Ensure<SpaceBackground>(go);
        SetRef(bg, "material", mat);
        SetInt(bg, "sortingOrder", BackgroundSortingOrder);
        go.SetActive(true);

        EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Debug.Log("[Background] Fondo espacial montado bajo la cámara. Tunea SpaceNebula.mat en el " +
            "Inspector (colores/velocidades/intensidad) y guarda (Ctrl+S).");
    }

    // Crea/carga el material desde el shader (patrón EnsureGlowMaterial de HudBuilder).
    private static Material EnsureMaterial()
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null) return null;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "SpaceNebula" };
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
        if (p == null) { Debug.LogWarning($"[Background] Campo '{field}' no encontrado en {target.GetType().Name}."); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetInt(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning($"[Background] Campo '{field}' no encontrado en {target.GetType().Name}."); return; }
        p.intValue = value;
        so.ApplyModifiedProperties();
    }
}
#endif
