using UnityEngine;

// Paredes invisibles de la arena. Construye 4 colliders sólidos en los bordes VISIBLES de la
// cámara ortográfica, leídos en runtime, así son resolución/aspect-independientes (clave para el
// cabinet, cuya resolución exacta puede que aún no conozcamos).
//
// El jugador (Rigidbody2D dinámico con CircleCollider2D sólido y Continuous) choca contra ellas y
// no puede salir de pantalla. Las balas son triggers (Hitbox) que solo dañan IDamageable, así que
// atraviesan las paredes y mueren por su 'range' (no las frenamos en v1).
//
// Además expone 'PlayArea' (el rectángulo jugable en mundo) para que otros sistemas (p. ej. el
// movimiento del boss) puedan clamparse dentro sin recalcular la cámara.
public class ArenaBounds : MonoBehaviour
{
    [Tooltip("Grosor de cada pared (hacia fuera de pantalla). Grueso = evita que algo rápido la atraviese.")]
    [SerializeField] private float wallThickness = 1f;

    [Tooltip("Margen hacia dentro: encoge la zona jugable respecto al borde visible (0 = justo el borde).")]
    [SerializeField] private float inset = 0f;

    [Tooltip("Cámara de referencia. Vacío = Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Capa de las paredes. Debe colisionar con la capa Player (Default sirve).")]
    [SerializeField] private int wallLayer = 0;

    // Rectángulo jugable en coordenadas de mundo (lo consultan otros sistemas, p. ej. BossMovement).
    public static Rect PlayArea { get; private set; }

    private void Awake()
    {
        Build();
    }

    [ContextMenu("Rebuild")]
    private void Build()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null || !cam.orthographic)
        {
            Debug.LogWarning("[ArenaBounds] No hay cámara ortográfica; no se crean paredes.");
            return;
        }

        // Limpia paredes previas (por si se reconstruye desde el menú contextual).
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        float half = cam.orthographicSize - inset;          // media altura visible
        float halfW = cam.orthographicSize * cam.aspect - inset; // media anchura visible
        Vector2 c = cam.transform.position;

        PlayArea = new Rect(c.x - halfW, c.y - half, halfW * 2f, half * 2f);

        float t = wallThickness;
        // Paredes solapando esquinas para que no queden huecos en las diagonales.
        CreateWall("Wall_Top", new Vector2(c.x, c.y + half + t * 0.5f), new Vector2(halfW * 2f + t * 2f, t));
        CreateWall("Wall_Bottom", new Vector2(c.x, c.y - half - t * 0.5f), new Vector2(halfW * 2f + t * 2f, t));
        CreateWall("Wall_Left", new Vector2(c.x - halfW - t * 0.5f, c.y), new Vector2(t, half * 2f + t * 2f));
        CreateWall("Wall_Right", new Vector2(c.x + halfW + t * 0.5f, c.y), new Vector2(t, half * 2f + t * 2f));
    }

    private void CreateWall(string wallName, Vector2 worldPos, Vector2 size)
    {
        var go = new GameObject(wallName) { layer = wallLayer };
        go.transform.SetParent(transform);
        go.transform.position = worldPos;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = size;   // collider sólido (no trigger): frena al jugador
    }
}
