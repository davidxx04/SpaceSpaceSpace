#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Herramienta de editor (carpeta "Editor": NO entra en la build) que viste el MENÚ de un clic:
//   - Fondo térmico (shader ThermalFlow vía SpaceBackground, patrón BackgroundBuilder).
//   - Fuente gruesa del proyecto (FontLibrary: Art/Fonts, Archivo Black por defecto) en título y botones.
//   - Botones NEÓN CRT: placa naranja procedural (shader NeonPlate) con scanlines que parpadean y
//     derivan, foco rojo parpadeante (NeonButtonFx + selección del EventSystem), letras blancas con
//     halación ligera y una capa de líneas por ENCIMA del texto (modo LinesOnly del mismo shader).
//   - Título: fuente nueva + degradado cromo cian->magenta. Sigue siendo el TMP_Text "Title"
//     (pensado para poder sustituirse por una imagen más adelante sin tocar nada más).
//
// Idempotente: re-ejecutable sin duplicar; materiales como assets tuneables (solo se configuran al
// CREARSE: los retoques del usuario en el Inspector sobreviven a re-ejecuciones).
//
// Uso: menú  SpaceSpaceSpace/UI/Build Menu Look  (con la escena Menu abierta). Luego Ctrl+S.
public static class MenuLookBuilder
{
    private const string ThermalShaderName = "SpaceSpaceSpace/ThermalFlow";
    private const string ThermalMaterialPath = "Assets/_Project/Art/MenuThermal.mat";
    private const string NeonShaderName = "SpaceSpaceSpace/NeonPlate";
    private const string NeonButtonMatPath = "Assets/_Project/Art/NeonButton.mat";
    private const string NeonLinesMatPath = "Assets/_Project/Art/NeonLines.mat";
    private const int BackgroundSortingOrder = -100;

    // Título como imagen: suelta aquí tu PNG (718x347) y el builder lo usa; si no está, cae al texto.
    private const string TitleSpritePath = "Assets/_Project/Art/UI/GameTitle.png";
    private const float TitleImageWidth = 480f;
    private const float TitleAspect = 718f / 347f;

    private static readonly Vector2 ButtonSize = new Vector2(320f, 64f);

    // Botones del menú: CIAN eléctrico (armoniza con la mitad superior del título azul->magenta y con
    // el fondo apagado; contrasta bien con el magenta del título). Hover = versión más caliente (->blanco).
    // Las teclas del nickname NO usan esto: siguen naranjas con su propio NeonKey.mat.
    private static readonly Color ButtonBaseColor = new Color(0.10f, 0.72f, 0.95f, 1f);
    private static readonly Color ButtonFocusColor = new Color(0.78f, 0.97f, 1f, 1f);

    // Path del scrim (lecho oscuro bajo la UI), generado por código si no existe.
    private const string ScrimPath = "Assets/_Project/Art/UI/menu_scrim.png";

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

        // --- Fondo térmico (SpaceBackground reutilizado con el material ThermalFlow) ---
        // MOODY por defecto: apagado y oscuro para que el fondo RECEDA y la UI sea lo más brillante
        // (arreglo del "a veces queda fatal": mata el amarillo chillón, sube viñeta, baja estrellas).
        Material thermalMat = EnsureMaterial(ThermalShaderName, ThermalMaterialPath, m =>
        {
            m.SetColor("_EdgeColor", new Color(0.42f, 0.06f, 0.05f));    // brasa roja apagada
            m.SetColor("_FringeColor", new Color(0.55f, 0.32f, 0.08f));  // ámbar apagado (mata el amarillo)
            m.SetColor("_BodyColor", new Color(0.06f, 0.34f, 0.40f));    // teal profundo
            m.SetFloat("_BlobIntensity", 0.62f);
            m.SetFloat("_FlareIntensity", 0.7f);
            m.SetFloat("_StarBrightness", 0.5f);
            m.SetFloat("_Vignette", 0.55f);
            m.SetFloat("_HueCycleSpeed", 0.005f);                        // vira más lento y sutil
        });
        if (thermalMat == null)
        {
            Debug.LogError($"[MenuLook] Shader '{ThermalShaderName}' no encontrado (¿compila?). Aborto.");
            return;
        }

        GameObject go = FindOrCreate("MenuBackground", cam.transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        // Cablear ANTES de que el [ExecuteAlways] monte su quad (evita el material fallback).
        go.SetActive(false);
        var bg = Ensure<SpaceBackground>(go);
        SetRef(bg, "material", thermalMat);
        SetInt(bg, "sortingOrder", BackgroundSortingOrder);
        go.SetActive(true);

        // La cámara despeja al color del cielo por si el quad no cubre algún borde.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = CameraClear;
        EditorUtility.SetDirty(cam);

        // --- Fuente del proyecto + preset de halación ---
        TMP_FontAsset font = FontLibrary.EnsureFont();
        Material glowPreset = FontLibrary.EnsureGlowPreset(font);
        if (font == null)
            Debug.LogWarning("[MenuLook] Sin fuente en Art/Fonts: título/botones seguirán con la de TMP.");

        // --- Título: imagen si existe GameTitle.png; si no, texto con fuente+degradado ---
        EnsureTitle(font, glowPreset);

        // --- Botones neón (cian; hover más caliente; borde de selección marcado) ---
        Material plateMat = EnsureMaterial(NeonShaderName, NeonButtonMatPath, m =>
        {
            m.SetColor("_BaseColor", ButtonBaseColor);
            m.SetColor("_FocusColor", ButtonFocusColor);
            m.SetFloat("_FocusBorderWidth", 4.5f);   // borde blanco de selección más gordo (se lee claro)
            m.SetFloat("_BorderFlickerMin", 0.5f);   // que no se apague demasiado al parpadear
        });
        Material linesMat = EnsureMaterial(NeonShaderName, NeonLinesMatPath, m =>
        {
            m.SetFloat("_LinesOnly", 1f);
            m.SetFloat("_LineStrength", 0.14f);   // muy sutil por encima de las letras
        });
        if (plateMat == null)
        {
            Debug.LogError($"[MenuLook] Shader '{NeonShaderName}' no encontrado (¿compila?). Botones sin placa.");
        }
        else
        {
            BuildNeonButton(startBtn, plateMat, linesMat, font, glowPreset);
            BuildNeonButton(FindButton("Btn_Leaderboard"), plateMat, linesMat, font, glowPreset);
        }

        Canvas menuCanvas = startBtn.GetComponentInParent<Canvas>();

        // Scrim oscuro central: lecho ESTABLE para la UI. Aunque el fondo animado cambie de color, el
        // título y los botones siempre tienen debajo una zona oscura => contraste constante.
        EnsureScrim(menuCanvas);

        // Composición compacta: agrupa título+botones centrados. Antes el ForceExpandHeight repartía
        // toda la altura y dejaba huecos enormes; ahora usa el tamaño real de cada hijo (LayoutElement).
        var vlg = menuCanvas.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.childControlWidth = false; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 22f;
            EditorUtility.SetDirty(vlg);
        }

        EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Debug.Log("[MenuLook] Menú vestido: fondo MOODY (MenuThermal.mat) + scrim central + botones CIAN " +
            "con hover caliente y borde de selección (NeonButton.mat) + layout compacto. Las teclas del " +
            "nickname (NeonKey.mat) NO se tocan. Para aplicar colores nuevos: BORRA MenuThermal.mat y " +
            "NeonButton.mat y re-ejecuta. Guarda (Ctrl+S).");
    }

    // Lecho oscuro radial detrás de la UI (hijo del canvas del menú, IGNORADO por el layout). Se
    // genera un PNG radial (negro en el centro -> transparente en los bordes) si no existe.
    private static void EnsureScrim(Canvas canvas)
    {
        Sprite sprite = EnsureScrimSprite();

        Transform t = canvas.transform.Find("MenuScrim");
        GameObject go = t != null ? t.gameObject
            : new GameObject("MenuScrim", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        if (t == null) go.transform.SetParent(canvas.transform, false);
        go.transform.SetSiblingIndex(0);   // detrás del título y los botones

        var le = Ensure<LayoutElement>(go);
        le.ignoreLayout = true;            // el VerticalLayoutGroup lo salta (no es un "botón" más)

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var img = Ensure<Image>(go);
        img.sprite = sprite;
        img.color = Color.white;
        img.raycastTarget = false;
    }

    private static Sprite EnsureScrimSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(ScrimPath);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/UI"))
            AssetDatabase.CreateFolder("Assets/_Project/Art", "UI");

        const int N = 256;
        const float MaxAlpha = 0.66f;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2((N - 1) * 0.5f, (N - 1) * 0.5f);
        float maxD = c.magnitude;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxD;   // 0 centro -> 1 esquina
                float a = MaxAlpha * (1f - Mathf.SmoothStep(0.2f, 1.0f, d));
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        tex.Apply();
        System.IO.File.WriteAllBytes(ScrimPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(ScrimPath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(ScrimPath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(ScrimPath);
    }

    // Convierte un Button de texto plano en placa neón: Image raíz con NeonPlate, label blanco con
    // fuente gruesa + halación, capa de líneas encima del texto y NeonButtonFx empujando estados.
    private static void BuildNeonButton(Button btn, Material plateMat, Material linesMat,
                                        TMP_FontAsset font, Material glowPreset)
    {
        if (btn == null) return;

        var rt = (RectTransform)btn.transform;
        rt.sizeDelta = ButtonSize;

        // Tamaño para el VerticalLayoutGroup compacto (ForceExpandHeight off usa el preferido).
        var btnLe = Ensure<LayoutElement>(btn.gameObject);
        btnLe.preferredWidth = ButtonSize.x;
        btnLe.preferredHeight = ButtonSize.y;

        // Placa: el Image raíz del botón pasa a pintar el shader (sin sprite; el color lo pone él).
        var plate = btn.GetComponent<Image>();
        if (plate == null) plate = btn.gameObject.AddComponent<Image>();
        plate.sprite = null;
        plate.color = Color.white;
        plate.material = plateMat;
        plate.raycastTarget = true;   // clicable con ratón en desarrollo

        // El shader pinta los estados; fuera el tinte de Unity.
        btn.transition = Selectable.Transition.None;
        EditorUtility.SetDirty(btn);

        // Label: blanco, centrado, fuente gruesa, halación ligera.
        var label = btn.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            if (font != null) label.font = font;
            if (glowPreset != null) label.fontSharedMaterial = glowPreset;
            label.color = Color.white;
            label.fontSize = 30f;
            label.characterSpacing = 4f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            Stretch((RectTransform)label.transform);
            EditorUtility.SetDirty(label);
        }

        // Capa de líneas por ENCIMA del texto (como en la referencia, las scanlines cruzan las letras).
        Image overlay = null;
        if (linesMat != null)
        {
            Transform t = btn.transform.Find("ScanOverlay");
            GameObject og = t != null ? t.gameObject : new GameObject("ScanOverlay", typeof(RectTransform), typeof(Image));
            if (t == null) og.transform.SetParent(btn.transform, false);
            og.transform.SetAsLastSibling();
            overlay = og.GetComponent<Image>();
            overlay.sprite = null;
            overlay.color = Color.white;
            overlay.material = linesMat;
            overlay.raycastTarget = false;
            Stretch((RectTransform)og.transform);
        }

        var fx = Ensure<NeonButtonFx>(btn.gameObject);
        SetRef(fx, "plate", plate);
        SetRef(fx, "linesOverlay", overlay);
        SetRef(fx, "label", label);   // la letra parpadea al enfocar
    }

    // Título flexible: imagen (GameTitle.png) o texto degradado. Conserva el TMP_Text "Title"
    // (solo lo oculta) para poder revertir sin reconstruir nada.
    private static void EnsureTitle(TMP_FontAsset font, Material glowPreset)
    {
        var title = FindText("Title");
        if (title == null) { Debug.LogWarning("[MenuLook] No encuentro el texto 'Title' (¿se renombró?)."); return; }

        Transform parent = title.transform.parent;
        Transform existingImg = parent != null ? parent.Find("TitleImage") : null;
        Sprite sprite = LoadTitleSprite();

        if (sprite != null)
        {
            GameObject go = existingImg != null
                ? existingImg.gameObject
                : new GameObject("TitleImage", typeof(RectTransform), typeof(Image));
            if (existingImg == null) go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(title.transform.GetSiblingIndex());   // donde estaba el texto

            var le = go.GetComponent<LayoutElement>();   // que el layout use su tamaño real
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = TitleImageWidth;
            le.preferredHeight = TitleImageWidth / TitleAspect;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(TitleImageWidth, TitleImageWidth / TitleAspect);

            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;

            title.gameObject.SetActive(false);   // conservado (oculto) por si se revierte
            EditorUtility.SetDirty(go);
            Debug.Log("[MenuLook] Título = imagen (GameTitle.png). El texto 'Title' queda oculto. " +
                "Ajusta tamaño/posición del 'TitleImage' a tu gusto.");
        }
        else
        {
            if (existingImg != null) Object.DestroyImmediate(existingImg.gameObject);
            title.gameObject.SetActive(true);
            if (font != null) title.font = font;
            if (glowPreset != null) title.fontSharedMaterial = glowPreset;
            title.color = Color.white;   // el degradado de vértice pone el color real
            title.enableVertexGradient = true;
            title.colorGradient = new VertexGradient(TitleTop, TitleTop, TitleBottom, TitleBottom);
            title.characterSpacing = 4f;
            EditorUtility.SetDirty(title);
            Debug.Log($"[MenuLook] Título con fuente/degradado. Para usar tu imagen, suelta el PNG (718x347) " +
                $"en '{TitleSpritePath}' y re-ejecuta.");
        }
    }

    // Carga el sprite del título asegurando que la textura se importa como Sprite. Null si no existe.
    private static Sprite LoadTitleSprite()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(TitleSpritePath) == null) return null;

        var imp = AssetImporter.GetAtPath(TitleSpritePath) as TextureImporter;
        if (imp != null && (imp.textureType != TextureImporterType.Sprite ||
                            imp.spriteImportMode != SpriteImportMode.Single))
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(TitleSpritePath);
    }

    // Crea/carga un material desde un shader (patrón BackgroundBuilder). 'onCreate' solo se aplica
    // al CREAR: los retoques del usuario en el Inspector sobreviven a re-ejecuciones del builder.
    public static Material EnsureMaterial(string shaderName, string path, System.Action<Material> onCreate)
    {
        var shader = Shader.Find(shaderName);
        if (shader == null) return null;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            onCreate?.Invoke(mat);
            AssetDatabase.CreateAsset(mat, path);
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
