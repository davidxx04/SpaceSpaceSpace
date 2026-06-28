using UnityEngine;

// Área de ataque del boss: zona rectangular POOLEADA con telegrafía de barra de relleno (§10.2/§12).
// Crea su visual procedural (quad 'prep' neutro + quad de 'relleno' que crece 0->1 anclado al borde
// negativo del eje elegido + glow additive opcional) y conduce un Hitbox hijo para el golpe.
// La dirige un BossAttackSO:
//   Configure -> [SetSize / posición / rotación cada frame si sigue] -> SetFill(0..1)
//   -> SetColor(impacto) -> ActivateHitbox/Deactivate -> despawn.
// El color del relleno lleva el verbo (rojo=esquiva / cian=parry); el 'prep' es neutro y común.
[DisallowMultipleComponent]
public class BossArea : MonoBehaviour, IPoolable
{
    [Tooltip("Hitbox hijo (BoxCollider2D trigger, targetLayers = Player) que se activa en el impacto.")]
    [SerializeField] private Hitbox hitbox;

    [Tooltip("Color del estado 'prep' (neutro): 'algo viene aquí'.")]
    [SerializeField] private Color prepColor = new Color(1f, 1f, 1f, 0.15f);

    [Tooltip("Orden de dibujado base de la zona (el glow va detrás, el relleno delante).")]
    [SerializeField] private int sortingOrder = -5;

    [Header("Glow (estético, opcional)")]
    [Tooltip("Añade un halo additive detrás del área, tintado al color actual. Apagado no cuesta nada.")]
    [SerializeField] private bool useGlow = false;

    [Tooltip("Cuánto más grande que el área es el halo (1 = igual).")]
    [SerializeField] private float glowScale = 1.15f;

    [Tooltip("Intensidad/alpha del halo additive.")]
    [SerializeField] private float glowIntensity = 0.6f;

    private SpriteRenderer prep;   // fondo neutro
    private SpriteRenderer fill;   // relleno (lleva el color de verbo / de impacto)
    private SpriteRenderer glow;   // halo additive opcional

    private Vector2 size = Vector2.one;
    private bool fillOnY = true;
    private float fillT;
    private Color currentColor = Color.red;

    private static Material glowMaterial;

    private void Awake()
    {
        if (prep == null) prep = CreateQuad("AreaPrep", sortingOrder, null);
        if (fill == null) fill = CreateQuad("AreaFill", sortingOrder + 1, null);
        if (useGlow && glow == null)
        {
            Material m = GetGlowMaterial();
            if (m != null) glow = CreateQuad("AreaGlow", sortingOrder - 1, m);
        }
        prep.color = prepColor;
    }

    // Prepara el área para un disparo: eje del relleno, tamaño, color de verbo; relleno a 0, hitbox off.
    // NO toca la rotación (la fijan los beams desde el SO); el reset al pool va en OnDespawned.
    public void Configure(Vector2 areaSize, Color verbColor, bool fillVertical)
    {
        fillOnY = fillVertical;
        currentColor = verbColor;

        ApplySize(areaSize);
        if (fill != null) fill.color = verbColor;
        if (prep != null) prep.color = prepColor;
        ApplyGlowColor();
        if (hitbox != null) hitbox.Deactivate();

        SetFill(0f);
    }

    // Redimensiona el área en vivo (para beams que siguen al jugador y cambian de longitud).
    public void SetSize(Vector2 areaSize)
    {
        ApplySize(areaSize);
        SetFill(fillT);   // re-aplica el relleno con el nuevo tamaño
    }

    // Progreso del relleno 0..1: ES el reloj de la telegrafía. Crece anclado al borde negativo del eje.
    public void SetFill(float t01)
    {
        fillT = Mathf.Clamp01(t01);
        if (fill == null) return;

        if (fillOnY)
        {
            fill.transform.localScale = new Vector3(size.x, size.y * fillT, 1f);
            fill.transform.localPosition = new Vector3(0f, size.y * (fillT - 1f) * 0.5f, 0f);
        }
        else
        {
            fill.transform.localScale = new Vector3(size.x * fillT, size.y, 1f);
            fill.transform.localPosition = new Vector3(size.x * (fillT - 1f) * 0.5f, 0f, 0f);
        }
    }

    // Recolorea el relleno (p. ej. al impacto) y el glow.
    public void SetColor(Color c)
    {
        currentColor = c;
        if (fill != null) fill.color = c;
        ApplyGlowColor();
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

    // Reset al volver al pool: rotación a identidad (los beams la rotan), relleno a 0, hitbox off.
    public void OnDespawned()
    {
        transform.rotation = Quaternion.identity;
        SetFill(0f);
        DeactivateHitbox();
    }

    private void ApplySize(Vector2 areaSize)
    {
        size = new Vector2(Mathf.Max(0.01f, areaSize.x), Mathf.Max(0.01f, areaSize.y));
        if (prep != null) prep.transform.localScale = size;
        if (hitbox != null) hitbox.SetBoxSize(size);
        if (glow != null) glow.transform.localScale = size * Mathf.Max(0.01f, glowScale);
    }

    private void ApplyGlowColor()
    {
        if (glow != null) glow.color = new Color(currentColor.r, currentColor.g, currentColor.b, glowIntensity);
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

    // Material additive del glow (reusa el shader de SpriteGlow). Cacheado static -> compartido por todas
    // las áreas. Si el shader no está, no hay glow (degrada con gracia).
    private static Material GetGlowMaterial()
    {
        if (glowMaterial == null)
        {
            Shader s = Shader.Find("SpaceSpaceSpace/SpriteGlow");
            if (s != null) glowMaterial = new Material(s) { name = "AreaGlow (auto)" };
        }
        return glowMaterial;
    }
}
