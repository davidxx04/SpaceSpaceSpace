using UnityEngine;

// Emisor de estelas (afterimages / "echo"): mientras está activo va dejando copias congeladas
// del sprite fuente que se desvanecen, creando el rastro típico de un dash. Es GENÉRICO y
// reutilizable (rol, dash futuro del jugador, dash del boss...). Lo configura un AfterimageData,
// así que el mismo emisor da feels distintos según el asset que se le pase.
public class AfterimageEmitter : MonoBehaviour
{
    [Tooltip("Sprite del que se toman las instantáneas (el de la nave). Si está vacío, se busca en hijos.")]
    [SerializeField] private SpriteRenderer source;

    private AfterimageData data;
    private bool emitting;
    private float spawnTimer;

    // Material de silueta sólida, creado una sola vez por código a partir del shader. Compartido
    // por todas las estelas: el color por estela va por SpriteRenderer.color (no por el material),
    // así no se generan instancias de material.
    private static Material solidMaterial;

    private void Awake()
    {
        if (source == null) source = GetComponentInChildren<SpriteRenderer>();
    }

    private static Material GetSolidMaterial()
    {
        if (solidMaterial == null)
        {
            Shader shader = Shader.Find("SpaceSpaceSpace/AfterimageSolid");
            if (shader != null) solidMaterial = new Material(shader) { name = "AfterimageSolid (auto)" };
        }
        return solidMaterial;
    }

    // Empieza a emitir con la configuración dada (p. ej. al entrar en el rol). null = no hace nada.
    public void StartEmitting(AfterimageData config)
    {
        data = config;
        emitting = config != null && source != null;
        spawnTimer = 0f;
        if (emitting) Spawn();   // una estela inmediata al arrancar
    }

    public void StopEmitting() => emitting = false;

    private void Update()
    {
        if (!emitting || data == null || source == null) return;

        float interval = Mathf.Max(0.001f, data.spawnInterval);
        spawnTimer += data.useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        while (spawnTimer >= interval)
        {
            spawnTimer -= interval;
            Spawn();
        }
    }

    // Crea una copia congelada del sprite en su pose actual (en espacio de mundo, SIN emparentar
    // a la nave, para que se quede atrás mientras la nave avanza).
    private void Spawn()
    {
        if (source.sprite == null) return;

        var go = new GameObject("Afterimage");
        Transform st = source.transform;
        go.transform.SetPositionAndRotation(st.position, st.rotation);
        go.transform.localScale = st.lossyScale;

        var ghost = go.AddComponent<SpriteRenderer>();
        ghost.flipX = source.flipX;
        ghost.flipY = source.flipY;
        ghost.sortingLayerID = source.sortingLayerID;
        ghost.sortingOrder = source.sortingOrder + data.sortingOffset;

        // Silueta sólida por defecto (shader auto). Si el SO trae un material, manda ese.
        // Último recurso (shader no encontrado): el material de la nave.
        Material mat = data.material != null ? data.material : GetSolidMaterial();
        ghost.sharedMaterial = mat != null ? mat : source.sharedMaterial;

        go.AddComponent<Afterimage>().Begin(source.sprite, data.color, data.alphaOverLife, data.lifetime, data.useUnscaledTime);
    }
}
