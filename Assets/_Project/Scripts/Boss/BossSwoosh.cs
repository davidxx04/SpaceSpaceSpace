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

    private SpriteRenderer line;   // línea fina de telegrafía
    private SpriteRenderer body;   // barra cuerpo (lleva el Hitbox)

    private void Awake()
    {
        if (line == null) line = CreateQuad("SwooshLine", sortingOrder, null);
        if (body == null) body = CreateQuad("SwooshBody", sortingOrder + 1, GetCrescentMaterial());
        line.enabled = false;
        body.enabled = false;
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
    // en X = avance), centrada en el objeto, y dimensiona el Hitbox.
    public void BeginBody(Vector2 dir, float width, float thickness, Color color)
    {
        AlignRoot(dir);
        if (line != null) line.enabled = false;
        if (body == null) return;

        Vector2 size = new Vector2(Mathf.Max(0.01f, thickness), Mathf.Max(0.01f, width)); // X=avance, Y=ancho
        body.transform.localScale = new Vector3(size.x, size.y, 1f);
        body.transform.localPosition = Vector3.zero;
        body.color = color;
        body.enabled = true;

        if (hitbox != null) hitbox.SetBoxSize(size);
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

    // Reset al volver al pool: rotación a identidad, visuales apagados, hitbox off.
    public void OnDespawned()
    {
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

    // Material del creciente (forma de "onda") reusando el shader SpriteSwoosh. Cacheado static ->
    // compartido por todos los swooshes (el color por-verbo va por SpriteRenderer.color, batching-friendly).
    // Si el shader no está, el cuerpo cae a su material por defecto (rectángulo) sin romperse.
    private static Material crescentMaterial;

    private static Material GetCrescentMaterial()
    {
        if (crescentMaterial == null)
        {
            Shader s = Shader.Find("SpaceSpaceSpace/SpriteSwoosh");
            if (s != null) crescentMaterial = new Material(s) { name = "SwooshCrescent (auto)" };
        }
        return crescentMaterial;
    }
}
