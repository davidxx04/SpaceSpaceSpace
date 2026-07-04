using UnityEngine;

// Primitivo SWOOSH del boss: una "onda" en forma de media luna que BARRE en una dirección, POOLEADA
// (IPoolable). Espejo de BossArea pero móvil. Visual procedural (sin arte): una LÍNEA fina de
// telegrafía (apunta en la dirección de lanzamiento, color de verbo) + un CUERPO con forma de
// CRECIENTE (shader SpriteSwoosh) que lleva el Hitbox. La coreografía (telegraph -> barrido) la
// dirige SwooshAttackSO moviendo el transform; aquí solo viven los visuales + el Hitbox.
//
// Convención de orientación: la RAÍZ se rota para que su +X local = dirección de avance. Los hijos
// (línea, cuerpo, Hitbox) quedan a rotación local identidad, así +X = avance (el bulto del creciente)
// e +Y = perpendicular (el eje largo punta-a-punta). El creciente se estira a la caja del hitbox.
[DisallowMultipleComponent]
public class BossSwoosh : MonoBehaviour, IPoolable
{
    [Tooltip("Hitbox hijo (BoxCollider2D trigger, targetLayers = Player) que se activa durante el barrido.")]
    [SerializeField] private Hitbox hitbox;

    [Tooltip("Grosor de la línea de telegrafía, en unidades.")]
    [SerializeField] private float telegraphThickness = 0.12f;

    [Tooltip("Orden de dibujado.")]
    [SerializeField] private int sortingOrder = -4;

    [Header("Rastro thermal")]
    [Tooltip("Vida del residuo térmico que deja el barrido, en segundos.")]
    [SerializeField] private float residueLifetime = 0.7f;

    [Tooltip("Distancia (u) entre fantasmas de residuo. Por distancia: estela uniforme sea cual sea travelSpeed/framerate.")]
    [SerializeField] private float residueSpacing = 0.45f;

    [Tooltip("Orden de dibujado del residuo (detrás del cuerpo y de la línea; BossArea usa -5).")]
    [SerializeField] private int residueSortingOrder = -6;

    private SpriteRenderer line;   // línea fina de telegrafía
    private SpriteRenderer body;   // barra cuerpo (lleva el Hitbox)

    // Estado de la emisión del rastro + parámetros por-swoosh que heredan los fantasmas.
    private MaterialPropertyBlock mpb;
    private bool emitting;
    private Vector3 lastPos;
    private float traveled;
    private float distSinceGhost;
    private float seed;
    private float palette;
    private Vector2 quadSize;
    private float bodyAlpha;

    private static readonly int HeatId = Shader.PropertyToID("_Heat");
    private static readonly int PaletteId = Shader.PropertyToID("_Palette");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int WorldOffsetId = Shader.PropertyToID("_WorldOffset");
    private static readonly int QuadSizeId = Shader.PropertyToID("_QuadSize");

    private void Awake()
    {
        if (line == null) line = CreateQuad("SwooshLine", sortingOrder, null);
        if (body == null) body = CreateQuad("SwooshBody", sortingOrder + 1, GetCrescentMaterial());
        line.enabled = false;
        body.enabled = false;
        mpb = new MaterialPropertyBlock();
    }

    // Muestra la línea fina de telegrafía: parte del origen y apunta en 'dir' una longitud 'length'.
    public void ShowTelegraph(Vector2 dir, float length, Color color)
    {
        AlignRoot(dir);
        if (line == null) return;

        float len = Mathf.Max(0.01f, length);
        line.transform.localScale = new Vector3(len, Mathf.Max(0.001f, telegraphThickness), 1f);
        line.transform.localPosition = new Vector3(len * 0.5f, 0f, 0f);   // crece hacia +X local (la dir)
        line.color = color;
        line.enabled = true;
        body.enabled = false;
    }

    // Cambia de telegrafía a CUERPO: barra perpendicular a 'dir' (ancho 'width' en Y, grosor 'thickness'
    // en X = avance), centrada en el objeto, y dimensiona el Hitbox. 'coldFire' elige la paleta del
    // fuego (cálida = esquiva, fría = parry): el matiz de verbo lo lleva la rampa del shader, no el
    // vertex color (que va blanco con el alpha maestro del SO).
    public void BeginBody(Vector2 dir, float width, float thickness, Color color, bool coldFire)
    {
        AlignRoot(dir);
        if (line != null) line.enabled = false;
        if (body == null) return;

        Vector2 size = new Vector2(Mathf.Max(0.01f, thickness), Mathf.Max(0.01f, width)); // X=avance, Y=ancho
        body.transform.localScale = new Vector3(size.x, size.y, 1f);
        body.transform.localPosition = Vector3.zero;
        body.color = new Color(1f, 1f, 1f, color.a);
        body.enabled = true;

        if (hitbox != null) hitbox.SetBoxSize(size);

        // Parámetros por-swoosh del fuego (reset OBLIGATORIO: instancia reusada del pool).
        seed = Random.Range(0f, 100f);
        palette = coldFire ? 1f : 0f;
        quadSize = size;
        bodyAlpha = color.a;

        mpb.SetFloat(HeatId, 1f);
        mpb.SetFloat(PaletteId, palette);
        mpb.SetFloat(SeedId, seed);
        mpb.SetFloat(WorldOffsetId, 0f);
        mpb.SetVector(QuadSizeId, quadSize);
        body.SetPropertyBlock(mpb);

        // Emisión del rastro (si el shader faltara, el cuerpo cae al rectángulo plano y no hay rastro).
        emitting = GetCrescentMaterial() != null;
        lastPos = transform.position;
        traveled = 0f;
        distSinceGhost = 0f;
    }

    // Emisión POR DISTANCIA del residuo mientras el SO traslada la raíz, y anclaje del marmoleado
    // al camino recorrido (_WorldOffset): el cuerpo "barre a través" de un campo de ruido fijo y
    // cada fantasma hereda exactamente el patrón que tenía debajo (handoff sin pop).
    private void Update()
    {
        if (!emitting || body == null || !body.enabled) return;

        Vector3 pos = transform.position;
        Vector3 delta = pos - lastPos;
        float len = delta.magnitude;
        if (len <= 0f) return;

        float spacing = Mathf.Max(0.05f, residueSpacing);
        Vector3 dir = delta / len;
        float consumed = 0f;
        while (distSinceGhost + (len - consumed) >= spacing)
        {
            float step = spacing - distSinceGhost;
            consumed += step;
            distSinceGhost = 0f;
            SpawnResidue(lastPos + dir * consumed, traveled + consumed);   // posición interpolada exacta
        }
        distSinceGhost += len - consumed;
        traveled += len;
        lastPos = pos;

        mpb.SetFloat(WorldOffsetId, traveled);
        body.SetPropertyBlock(mpb);
    }

    // Cierre natural del barrido: deja un ÚLTIMO fantasma en la posición final para que el cuerpo
    // "se enfríe" en el sitio en vez de desaparecer de golpe. Llamar antes de PoolManager.Despawn.
    public void EndBody()
    {
        if (emitting) SpawnResidue(transform.position, traveled);
        emitting = false;
        if (body != null) body.enabled = false;
    }

    public void ActivateHitbox(DamageInfo info)
    {
        if (hitbox != null) hitbox.Activate(info);
    }

    public void DeactivateHitbox()
    {
        if (hitbox != null) hitbox.Deactivate();
    }

    public void OnSpawned() { }

    // Reset al volver al pool: rotación a identidad, visuales apagados, hitbox off. Sin fantasma
    // final aquí: este camino también es el stagger/limpieza de escena (ReleaseActive), donde no
    // toca; los residuos ya emitidos terminan de enfriarse solos (visuales puros, sin hitbox).
    public void OnDespawned()
    {
        emitting = false;
        transform.rotation = Quaternion.identity;
        if (line != null) line.enabled = false;
        if (body != null) body.enabled = false;
        DeactivateHitbox();
    }

    // Rota la raíz para que su +X local apunte en 'dir' (los hijos quedan a local identidad).
    private void AlignRoot(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    private SpriteRenderer CreateQuad(string n, int order, Material mat)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PrimitiveQuad.Unit;
        sr.sortingOrder = order;
        if (mat != null) sr.sharedMaterial = mat;
        return sr;
    }

    // --- Rastro de residuo térmico (fantasmas pooleados, sin prefab) ---

    // Clave única del pool key+factory; los ~28 fantasmas por barrido se reusan entre swooshes.
    private static readonly object ResiduePoolKey = typeof(SwooshResidue);
    private const int ResiduePoolMax = 64;   // pico real ~31 concurrentes (14/0.45 u/s * 0.7 s + solape)

    private void SpawnResidue(Vector3 pos, float worldOffset)
    {
        GameObject go = PoolManager.Spawn(ResiduePoolKey, CreateResidueGhost, pos, transform.rotation, ResiduePoolMax);
        if (go == null || !go.TryGetComponent(out SwooshResidue ghost)) return;
        ghost.Begin(body.transform.localScale, palette, seed, worldOffset, quadSize,
                    residueLifetime, bodyAlpha, residueSortingOrder);
    }

    private static GameObject CreateResidueGhost()
    {
        var go = new GameObject("SwooshResidue");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = PrimitiveQuad.Unit;
        Material mat = GetCrescentMaterial();
        if (mat != null) sr.sharedMaterial = mat;
        sr.enabled = false;
        go.AddComponent<SwooshResidue>();
        return go;
    }

    // Material del creciente (forma de "onda") reusando el shader SpriteSwoosh. Cacheado static ->
    // compartido por todos los swooshes Y sus fantasmas de residuo (lo per-swoosh/per-fantasma va
    // por MaterialPropertyBlock). Si el shader no está, el cuerpo cae a su material por defecto
    // (rectángulo) sin romperse y no se emite rastro.
    private static Material crescentMaterial;

    internal static Material GetCrescentMaterial()
    {
        if (crescentMaterial == null)
        {
            Shader s = Shader.Find("SpaceSpaceSpace/SpriteSwoosh");
            if (s != null) crescentMaterial = new Material(s) { name = "SwooshCrescent (auto)" };
        }
        return crescentMaterial;
    }
}
