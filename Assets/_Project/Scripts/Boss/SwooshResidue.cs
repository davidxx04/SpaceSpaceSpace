using UnityEngine;

// Fantasma de residuo térmico del swoosh: un quad creciente CONGELADO en el mundo que "se enfría"
// (empuja _Heat 1->0 por MaterialPropertyBlock sobre el material compartido del creciente) y se
// auto-devuelve al pool al agotar su vida. El shader hace el resto: al bajar _Heat todo el color se
// desliza por la banda FLIR (cian -> índigo -> negro) con disolución y grano crecientes.
//
// Lo emite BossSwoosh (pool key+factory, sin prefab); no se configura a mano. Hereda del swoosh
// padre _Palette/_Seed/_WorldOffset/_QuadSize CONGELADOS al nacer: el seed compartido y el offset
// de camino son lo que hace que la estela sea continua (fantasma = el patrón que el cuerpo tenía
// debajo en ese punto). El ruido sigue animando con _Time (brasas vivas), solo el calor decae.
[DisallowMultipleComponent]
public class SwooshResidue : MonoBehaviour, IPoolable
{
    // Enfría rápido al principio y remolonea en los fríos (cian/índigo), el look FLIR dominante.
    private const float CoolEase = 1.25f;

    private static readonly int HeatId = Shader.PropertyToID("_Heat");
    private static readonly int PaletteId = Shader.PropertyToID("_Palette");
    private static readonly int SeedId = Shader.PropertyToID("_Seed");
    private static readonly int WorldOffsetId = Shader.PropertyToID("_WorldOffset");
    private static readonly int QuadSizeId = Shader.PropertyToID("_QuadSize");

    private SpriteRenderer sr;
    private MaterialPropertyBlock mpb;
    private float age;
    private float lifetime;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        mpb = new MaterialPropertyBlock();
    }

    public void Begin(Vector3 scale, float palette, float seed, float worldOffset,
                      Vector2 quadSize, float life, float alpha, int order)
    {
        transform.localScale = scale;
        age = 0f;
        lifetime = Mathf.Max(0.0001f, life);

        if (sr == null) return;
        sr.color = new Color(1f, 1f, 1f, alpha);
        sr.sortingOrder = order;

        mpb.SetFloat(HeatId, 1f);
        mpb.SetFloat(PaletteId, palette);
        mpb.SetFloat(SeedId, seed);
        mpb.SetFloat(WorldOffsetId, worldOffset);
        mpb.SetVector(QuadSizeId, quadSize);
        sr.SetPropertyBlock(mpb);
        sr.enabled = true;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);

        if (sr != null)
        {
            mpb.SetFloat(HeatId, Mathf.Pow(1f - t, CoolEase));
            sr.SetPropertyBlock(mpb);
        }

        if (age >= lifetime) PoolManager.Despawn(gameObject);
    }

    public void OnSpawned() { }

    // Reuso limpio del pool: apagado hasta el próximo Begin.
    public void OnDespawned()
    {
        if (sr != null) sr.enabled = false;
    }
}
