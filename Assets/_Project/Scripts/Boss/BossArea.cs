using UnityEngine;

// Área de ataque del boss: zona rectangular POOLEADA con telegrafía de "cast" (§10.2/§12).
// Crea su visual procedural (quad 'prep' neutro + quad 'fill' a tamaño completo con el shader
// AreaCast: anillos cuadrados de puntos que nacen en el centro y tocan las 4 paredes exactamente
// al impacto, más una línea de barrido que cruza el eje elegido con el mismo reloj + glow additive
// opcional) y conduce un Hitbox hijo para el golpe. La dirige un BossAttackSO:
//   Configure -> [SetSize / posición / rotación cada frame si sigue] -> SetFill(0..1 = _Progress)
//   -> SetColor(impacto = losa sólida) -> ActivateHitbox/Deactivate -> despawn.
// El verbo va por paleta del shader (cálida=esquiva / fría=parry, elegida en Configure); el 'prep'
// es neutro y común. Si el shader faltara, degrada al wipe clásico (la telegrafía sigue jugable).
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

    [Header("Cast (telegrafía de anillos)")]
    [Tooltip("Material del shader AreaCast para tunear el look en el Inspector (tamaño/densidad de " +
             "puntos, colores de fondo y rampas, anillos, barrido). Editable en Play y persiste. " +
             "Vacío = se crea uno por código con los defaults del shader.")]
    [SerializeField] private Material castMaterialAsset;

    private SpriteRenderer prep;   // fondo neutro
    private SpriteRenderer fill;   // relleno (lleva el color de verbo / de impacto)
    private SpriteRenderer glow;   // halo additive opcional

    private Vector2 size = Vector2.one;
    private bool fillOnY = true;
    private float fillT;
    private Color currentColor = Color.red;

    // Cast procedural (AreaCast): estado por instancia empujado por MaterialPropertyBlock.
    private MaterialPropertyBlock mpb;
    private bool useCast;

    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int SweepAxisId = Shader.PropertyToID("_SweepAxis");
    private static readonly int PaletteId = Shader.PropertyToID("_Palette");
    private static readonly int FlashId = Shader.PropertyToID("_Flash");
    private static readonly int QuadSizeId = Shader.PropertyToID("_QuadSize");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");

    private static Material glowMaterial;
    private static Material castMaterial;

    private void Awake()
    {
        Material cast = ResolveCastMaterial();
        if (prep == null) prep = CreateQuad("AreaPrep", sortingOrder, null);
        if (fill == null) fill = CreateQuad("AreaFill", sortingOrder + 1, cast);
        if (useGlow && glow == null)
        {
            Material m = GetGlowMaterial();
            if (m != null) glow = CreateQuad("AreaGlow", sortingOrder - 1, m);
        }
        prep.color = prepColor;
        useCast = cast != null;
        mpb = new MaterialPropertyBlock();
    }

    // Prepara el área para un disparo: eje del barrido, tamaño, color de verbo y paleta del cast
    // (cálida = esquiva, fría = parry); progreso a 0, hitbox off. NO toca la rotación (la fijan
    // los beams desde el SO); el reset al pool va en OnDespawned.
    public void Configure(Vector2 areaSize, Color verbColor, bool fillVertical, bool coldPalette)
    {
        fillOnY = fillVertical;
        currentColor = verbColor;

        ApplySize(areaSize);
        if (fill != null)
        {
            // Con cast, el matiz de verbo vive en la rampa del shader; el vertex color lleva solo
            // el alpha maestro del SO (mismo criterio que BossSwoosh.BeginBody).
            fill.color = useCast ? new Color(1f, 1f, 1f, verbColor.a) : verbColor;
        }
        // Con cast, el fondo del área lo pinta el shader (azul/rojo oscuro por verbo) y el prep
        // gris se oculta; el prep solo actúa en el fallback sin shader.
        if (prep != null) prep.color = useCast ? Color.clear : prepColor;
        ApplyGlowColor();
        if (hitbox != null) hitbox.Deactivate();

        if (useCast && fill != null)
        {
            // Reset COMPLETO del MPB: instancia reusada del pool.
            mpb.SetFloat(ProgressId, 0f);
            mpb.SetFloat(SweepAxisId, fillVertical ? 1f : 0f);
            mpb.SetFloat(PaletteId, coldPalette ? 1f : 0f);
            mpb.SetFloat(FlashId, 0f);
            mpb.SetFloat(SeedId, Random.Range(0f, 100f));
            mpb.SetVector(QuadSizeId, size);
            fill.SetPropertyBlock(mpb);
        }

        SetFill(0f);
    }

    // Redimensiona el área en vivo (para beams que siguen al jugador y cambian de longitud).
    public void SetSize(Vector2 areaSize)
    {
        ApplySize(areaSize);
        SetFill(fillT);   // re-aplica el relleno con el nuevo tamaño
    }

    // Progreso 0..1: ES el reloj de la telegrafía. Con cast, empuja _Progress (los anillos nacen
    // en el centro y el líder toca las 4 paredes exactamente a 1). Sin shader, el wipe clásico.
    public void SetFill(float t01)
    {
        fillT = Mathf.Clamp01(t01);
        if (fill == null) return;

        if (useCast)
        {
            mpb.SetFloat(ProgressId, fillT);
            fill.SetPropertyBlock(mpb);
            return;
        }

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

    // Recolorea el relleno y el glow. Los SO lo llaman AL IMPACTO: con cast además enciende _Flash,
    // que funde los anillos a losa SÓLIDA (blanco x vertex rgb = impactColor) — la zona de daño
    // se lee inequívoca durante impactSeconds.
    public void SetColor(Color c)
    {
        currentColor = c;
        if (fill != null) fill.color = c;
        ApplyGlowColor();

        if (useCast && fill != null)
        {
            mpb.SetFloat(FlashId, 1f);
            fill.SetPropertyBlock(mpb);
        }
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

    // Reset al volver al pool: rotación a identidad (los beams la rotan), progreso/flash a 0, hitbox off.
    public void OnDespawned()
    {
        transform.rotation = Quaternion.identity;
        if (useCast && fill != null && mpb != null)
        {
            mpb.SetFloat(FlashId, 0f);
            mpb.SetFloat(ProgressId, 0f);
            fill.SetPropertyBlock(mpb);
        }
        SetFill(0f);
        DeactivateHitbox();
    }

    private void ApplySize(Vector2 areaSize)
    {
        size = new Vector2(Mathf.Max(0.01f, areaSize.x), Mathf.Max(0.01f, areaSize.y));
        if (prep != null) prep.transform.localScale = size;
        if (hitbox != null) hitbox.SetBoxSize(size);
        if (glow != null) glow.transform.localScale = size * Mathf.Max(0.01f, glowScale);

        // Con cast, el quad de relleno cubre SIEMPRE el área completa (el "llenado" lo pinta el
        // shader) y necesita el tamaño en mundo para que los puntos salgan redondos y con espaciado
        // uniforme aunque el rect cambie en vivo (beams con trackPlayer via SetSize).
        if (useCast && fill != null)
        {
            fill.transform.localScale = new Vector3(size.x, size.y, 1f);
            fill.transform.localPosition = Vector3.zero;
            if (mpb != null)
            {
                mpb.SetVector(QuadSizeId, size);
                fill.SetPropertyBlock(mpb);
            }
        }
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

    // Material del cast (anillos de puntos + barrido). Preferimos el ASSET serializado (AreaCast.mat)
    // para tunear el look desde el Inspector y que persista; si no hay asset, se crea uno por código
    // con los defaults del shader. Cacheado static -> compartido por todas las áreas (lo per-instancia
    // va por MaterialPropertyBlock; el asset se usa como sharedMaterial, nunca se instancia -> sin
    // fugas). Si ni asset ni shader están, useCast queda a false y SetFill degrada al wipe clásico.
    private Material ResolveCastMaterial()
    {
        if (castMaterial != null) return castMaterial;

        if (castMaterialAsset != null)
        {
            castMaterial = castMaterialAsset;
            return castMaterial;
        }

        Shader s = Shader.Find("SpaceSpaceSpace/AreaCast");
        if (s != null) castMaterial = new Material(s) { name = "AreaCast (auto)" };
        return castMaterial;
    }
}
