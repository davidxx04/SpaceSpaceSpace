#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Herramienta de editor (NO entra en la build: vive en una carpeta "Editor") que monta y CABLEA el
// HUD de un clic, para no tener que pelearse con las "cajas" (RectTransform) ni arrastrar refs a mano.
//
// Estética: barras pixel-art FINAS "pro". Genera sus sprites por código (texturas pequeñas con point
// filtering) y los guarda como assets reales en Art/UI/, así el look queda "ligeramente pixelado"
// SOLO en la UI (no toca cámara ni sprites del juego). Cada barra son hasta 4 capas:
//   Bg (canal hundido) + Fill (relleno metálico, Image Filled) + Ticks (segmentos angulares,
//   Image Tiled) + Frame (bisel 9-slice por encima, que tapa los bordes del relleno).
//
// IDEMPOTENTE y AUTO-SANADOR: reutiliza los GameObjects existentes (HUD_PlayerHP / HUD_Energy /
// HUD_BossHP / HUD_Timer) y, al reconstruir cada barra, BORRA cualquier capa que no sea de la lista
// conocida (limpia restos de versiones antiguas como BarBackground/BarFill, que se quedaban encima
// tapando las barras nuevas). Asegura/cablea los sistemas que el HUD necesita (Health y EnergyTank
// del player, refs del MatchController).
//
// Uso: menú  SpaceSpaceSpace > Build HUD  (con la escena Game abierta). Luego Ctrl+S.
public static class HudBuilder
{
    private const string MenuPath = "SpaceSpaceSpace/Build HUD";

    // ---- Tuning (todo ajustable; re-ejecuta la herramienta para reaplicar) ----
    // Pixelado: PPU del sprite. El Canvas usa 100 px/unidad de referencia, así que cada píxel del
    // sprite ocupa ~ (100 / PixelPpu) px en pantalla. 64 => pixelado fino. Baja = más chunky.
    private const float PixelPpu = 64f;

    private const int FrameTexSize = 12;        // marco del player (fino)
    private const int FrameBorder = 2;          // grosor 9-slice del marco del player
    private const int BossFrameTexSize = 16;    // marco del boss (más grueso, tipo ref.2)
    private const int BossFrameBorder = 3;

    private const int FillTexW = 4, FillTexH = 16;

    private const bool AddTicks = true;         // capa de segmentos angulares
    private const int TickTexSize = 8;          // tile de la muesca diagonal
    private const float TickPpuMult = 1f;       // densidad de los segmentos (mayor = más juntos)

    // Paleta del marco del player (azulada, con profundidad).
    private static readonly Color Outline = new Color(0.05f, 0.06f, 0.08f, 1f);
    private static readonly Color FrameLight = new Color(0.42f, 0.47f, 0.58f, 1f);
    private static readonly Color FrameDark = new Color(0.16f, 0.18f, 0.24f, 1f);

    // Paleta del marco del boss (más oscuro y pesado).
    private static readonly Color BossOutline = new Color(0.02f, 0.02f, 0.03f, 1f);
    private static readonly Color BossFrameLight = new Color(0.28f, 0.30f, 0.36f, 1f);
    private static readonly Color BossFrameDark = new Color(0.09f, 0.10f, 0.14f, 1f);

    private static readonly Color BgEmpty = new Color(0.07f, 0.08f, 0.11f, 0.94f);
    private static readonly Color TickColor = new Color(0f, 0f, 0f, 0.34f);  // muesca semitransparente

    private static readonly Color HpColor = new Color(0.90f, 0.24f, 0.27f, 1f);
    private static readonly Color EnergyColor = new Color(0.28f, 0.78f, 1f, 1f);
    private static readonly Color BossColor = new Color(0.95f, 0.45f, 0.14f, 1f);

    private const string ArtDir = "Assets/_Project/Art/UI";
    private const string FontDir = "Assets/_Project/Art/Fonts";
    private const string FramePath = ArtDir + "/hud_frame.png";
    private const string BossFramePath = ArtDir + "/hud_frame_boss.png";
    private const string FillPath = ArtDir + "/hud_fill.png";
    private const string TicksPath = ArtDir + "/hud_ticks.png";

    private static Sprite frameSprite, bossFrameSprite, fillSprite, ticksSprite;

    [MenuItem(MenuPath)]
    public static void Build()
    {
        var player = Object.FindObjectOfType<PlayerController>();
        if (player == null) { Debug.LogError("[HUD] No hay PlayerController en la escena. Abre la escena Game."); return; }

        EnsureSprites();

        var boss = Object.FindObjectOfType<BossController>();
        var timer = Object.FindObjectOfType<MatchTimer>();
        var match = Object.FindObjectOfType<MatchController>();

        // --- Sistemas que el HUD necesita ---
        var receiver = player.GetComponent<PlayerDamageReceiver>();
        var playerHealth = Ensure<Health>(player.gameObject);
        var energy = Ensure<EnergyTank>(player.gameObject);

        if (receiver != null) SetRef(receiver, "health", playerHealth);
        if (receiver != null) SetRef(energy, "receiver", receiver);

        Health bossCore = boss != null ? boss.Core : null;
        if (boss != null && bossCore == null)
            Debug.LogWarning("[HUD] El BossController no tiene 'core' (Health) asignado: la barra del boss quedará sin fuente. Asígnalo en el Inspector del Boss.");

        if (match != null)
        {
            SetRef(match, "playerHealth", playerHealth);
            SetRef(match, "playerReceiver", receiver);
            SetRef(match, "boss", boss);
            SetRef(match, "timer", timer);
        }
        else Debug.LogWarning("[HUD] No hay MatchController en la escena (el resultado de partida no se coordinará).");

        if (timer == null) Debug.LogWarning("[HUD] No hay MatchTimer en la escena (el reloj no contará).");

        Canvas canvas = FindHudCanvas();

        // --- Barras (pequeñas y finas) ---
        StatBar hpBar = BuildBar("HUD_PlayerHP", canvas, new Vector2(0f, 1f),
                                 new Vector2(16f, -14f), new Vector2(160f, 13f), HpColor, frameSprite);
        var hpBinder = Ensure<HealthBarBinder>(hpBar.gameObject);
        SetRef(hpBinder, "health", playerHealth);
        SetRef(hpBinder, "bar", hpBar);

        StatBar enBar = BuildBar("HUD_Energy", canvas, new Vector2(0f, 1f),
                                 new Vector2(16f, -33f), new Vector2(138f, 10f), EnergyColor, frameSprite);
        var enBinder = Ensure<EnergyBarBinder>(enBar.gameObject);
        SetRef(enBinder, "tank", energy);
        SetRef(enBinder, "bar", enBar);

        StatBar bossBar = BuildBar("HUD_BossHP", canvas, new Vector2(0.5f, 0f),
                                   new Vector2(0f, 30f), new Vector2(360f, 13f), BossColor, bossFrameSprite);
        var bossBinder = Ensure<HealthBarBinder>(bossBar.gameObject);
        SetRef(bossBinder, "health", bossCore);
        SetRef(bossBinder, "bar", bossBar);

        BuildTimer("HUD_Timer", canvas, timer);

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Debug.Log("[HUD] HUD pixel-art (fino) montado, limpiado y cableado. Revisa la consola por si hay refs sin fuente y guarda (Ctrl+S).");
    }

    // Construye una barra (Bg + Fill + Ticks? + Frame) en su contenedor (lo crea si falta) y LIMPIA
    // cualquier capa antigua que no pertenezca (restos de versiones previas: BarBackground/BarFill).
    private static StatBar BuildBar(string name, Canvas canvas, Vector2 corner,
                                    Vector2 anchoredPos, Vector2 size, Color fillColor, Sprite frame)
    {
        GameObject container = EnsureContainer(name, canvas.transform);
        var rt = container.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = corner;
        rt.pivot = corner;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        // Capas (orden de hermanos = orden de dibujado; el marco va arriba y tapa los bordes del relleno).
        int layer = 0;
        EnsureLayer(container.transform, "Bg", layer++, fillSprite, BgEmpty, Image.Type.Simple);

        var fill = EnsureLayer(container.transform, "Fill", layer++, fillSprite, fillColor, Image.Type.Filled);
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;

        if (AddTicks)
        {
            var ticks = EnsureLayer(container.transform, "Ticks", layer++, ticksSprite, Color.white, Image.Type.Tiled);
            ticks.pixelsPerUnitMultiplier = TickPpuMult;   // densidad de los segmentos
        }

        EnsureLayer(container.transform, "Frame", layer++, frame, Color.white, Image.Type.Sliced);

        CleanupLayers(container.transform);   // borra capas viejas que tapaban la barra

        var statBar = Ensure<StatBar>(container);
        SetRef(statBar, "fill", fill);
        return statBar;
    }

    // Borra cualquier hijo del contenedor que no sea una de las capas conocidas (auto-saneo).
    private static void CleanupLayers(Transform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            string n = container.GetChild(i).name;
            bool known = n == "Bg" || n == "Fill" || n == "Frame" || (AddTicks && n == "Ticks");
            if (!known) Object.DestroyImmediate(container.GetChild(i).gameObject);
        }
    }

    private static void BuildTimer(string name, Canvas canvas, MatchTimer timer)
    {
        GameObject container = EnsureContainer(name, canvas.transform);
        var rt = container.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(120f, 40f);
        rt.anchoredPosition = new Vector2(-16f, -14f);

        TMP_FontAsset pixelFont = FindPixelFont();

        var label = container.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
        {
            var go = new GameObject("TimerLabel", typeof(RectTransform));
            go.transform.SetParent(container.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Right;
            tmp.text = "6:00";
            var lrt = tmp.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            label = tmp;
        }

        // Fuente: pixel si la hay; si no, la por defecto de TMP.
        label.font = pixelFont != null ? pixelFont
                   : (label.font != null ? label.font : TMP_Settings.defaultFontAsset);
        label.fontSize = 28f;

        var view = Ensure<TimerTextView>(container);
        SetRef(view, "timer", timer);
        SetRef(view, "label", label);
    }

    // Busca la primera fuente pixel (TMP_FontAsset) en Art/Fonts. Si no hay, avisa con los pasos.
    private static TMP_FontAsset FindPixelFont()
    {
        if (!AssetDatabase.IsValidFolder(FontDir))
        {
            Debug.LogWarning($"[HUD] Sin fuente pixel: no existe la carpeta {FontDir}. " +
                "Descarga 'Press Start 2P' (.ttf, gratis en Google Fonts), créala como TMP Font Asset " +
                "ahí y re-ejecuta Build HUD. De momento el timer usa la fuente por defecto.");
            return null;
        }

        var guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontDir });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[HUD] Sin TMP_FontAsset en {FontDir}. Suelta un .ttf pixel (p.ej. " +
                "'Press Start 2P') y haz clic derecho > Create > TextMeshPro > Font Asset (Sampling " +
                "Point Size, filtro Point). Re-ejecuta Build HUD y lo cogerá. Timer con fuente por defecto.");
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    // --- Capas / contenedores ---

    private static GameObject EnsureContainer(string name, Transform parent)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) go = new GameObject(name, typeof(RectTransform));
        if (go.GetComponent<RectTransform>() == null) go.AddComponent<RectTransform>();
        if (go.transform.parent != parent) go.transform.SetParent(parent, false);
        return go;
    }

    // Crea/configura una capa Image que ocupa todo el contenedor, con su orden de hermano.
    private static Image EnsureLayer(Transform parent, string name, int siblingIndex,
                                     Sprite sprite, Color color, Image.Type type)
    {
        Transform t = parent.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        if (t == null) go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        go.transform.SetSiblingIndex(siblingIndex);

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = type;
        img.raycastTarget = false;   // HUD no recibe clics
        return img;
    }

    // --- Generación de sprites pixel-art ---

    private static void EnsureSprites()
    {
        if (!Directory.Exists(ArtDir)) Directory.CreateDirectory(ArtDir);

        WriteFrameTexture(FramePath, FrameTexSize, FrameBorder, Outline, FrameLight, FrameDark);
        WriteFrameTexture(BossFramePath, BossFrameTexSize, BossFrameBorder, BossOutline, BossFrameLight, BossFrameDark);
        WriteFillTexture(FillPath);
        WriteTicksTexture(TicksPath);

        AssetDatabase.Refresh();
        ImportAsSprite(FramePath, FrameBorder);
        ImportAsSprite(BossFramePath, BossFrameBorder);
        ImportAsSprite(FillPath, 0);
        ImportAsSprite(TicksPath, 0);

        frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
        bossFrameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BossFramePath);
        fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FillPath);
        ticksSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TicksPath);
    }

    // Marco biselado: contorno + bisel (claro arriba/izq, oscuro abajo/der), interior transparente.
    private static void WriteFrameTexture(string path, int n, int border, Color outline, Color light, Color dark)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int dl = x, dr = n - 1 - x, db = y, dt = n - 1 - y;
                int m = Mathf.Min(Mathf.Min(dl, dr), Mathf.Min(db, dt));
                Color c;
                if (m >= border) c = Color.clear;                 // interior transparente
                else if (m == 0) c = outline;                     // contorno
                else c = (dt == m || dl == m) ? light : dark;     // bisel
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    // Relleno blanco con bandas verticales nítidas (brillo especular arriba, cuerpo, sombra abajo).
    // Se tiñe con el color de la barra → aspecto de tubo metálico fino (no "masa" blanda).
    private static void WriteFillTexture(string path)
    {
        int w = FillTexW, h = FillTexH;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            float lum;
            if (y == h - 1) lum = 1.00f;        // brillo especular (1px arriba)
            else if (y >= h - 3) lum = 0.90f;   // brillo alto
            else if (y == 0) lum = 0.45f;       // sombra base (1px abajo)
            else if (y <= 2) lum = 0.62f;       // sombra
            else lum = 0.80f;                   // cuerpo saturado
            Color c = new Color(lum, lum, lum, 1f);
            for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    // Tile con muesca diagonal (2px, de abajo-izq a arriba-der). Tileado horizontalmente divide la
    // barra en celdas anguladas (el look segmentado de la ref.1). El resto, transparente.
    private static void WriteTicksTexture(string path)
    {
        int n = TickTexSize;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int d = x - y;
                tex.SetPixel(x, y, (d == 0 || d == 1) ? TickColor : Color.clear);
            }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    private static void ImportAsSprite(string path, int border)
    {
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.filterMode = FilterMode.Point;        // <- pixelado (sin suavizar)
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.alphaIsTransparency = true;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.spritePixelsPerUnit = PixelPpu;

        var s = new TextureImporterSettings();
        imp.ReadTextureSettings(s);
        s.spriteBorder = new Vector4(border, border, border, border);
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteGenerateFallbackPhysicsShape = false;
        imp.SetTextureSettings(s);
        imp.SaveAndReimport();
    }

    // --- Helpers genéricos ---

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
        if (prop == null) { Debug.LogWarning($"[HUD] Campo '{field}' no encontrado en {target.GetType().Name}."); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static Canvas FindHudCanvas()
    {
        var go = GameObject.Find("CanvasHUDInGame");
        if (go != null)
        {
            var c = go.GetComponent<Canvas>();
            if (c != null) return c;
        }
        foreach (var c in Object.FindObjectsOfType<Canvas>())
            if (c.name != "CanvasPopups") return c;

        var ng = new GameObject("CanvasHUDInGame", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var nc = ng.GetComponent<Canvas>();
        nc.renderMode = RenderMode.ScreenSpaceOverlay;
        return nc;
    }
}
#endif
