using System.Collections;
using UnityEngine;

// Ataque CRUZ: cubre el eje X completo (barra horizontal) y el eje Y completo (barra vertical) a la vez,
// cruzadas en el centro de la arena. Seguro = las 4 esquinas → te FUERZA a dashear a una esquina.
// Reutiliza dos BossArea (pooleadas) telegrafiadas en paralelo con una sola barra de relleno. No parreable.
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Attacks/Cross Area", fileName = "CrossAreaAttack")]
public class CrossAreaAttackSO : BossAttackSO
{
    [Header("Forma")]
    [Tooltip("Grosor de cada barra de la cruz, en unidades.")]
    public float thickness = 2.5f;

    [Header("Tiempos")]
    [Tooltip("Duración del relleno = telegrafía (marca cuándo cae el golpe), en segundos.")]
    public float fillSeconds = 0.7f;

    [Tooltip("Cuánto permanece activo el golpe tras llenarse, en segundos.")]
    public float impactSeconds = 0.12f;

    [Header("Daño / color")]
    public float damage = 12f;

    [Range(0f, 1f)]
    public float alpha = 0.7f;

    public Color dodgeColor = new Color(1f, 0.28f, 0.05f, 1f);
    public Color impactColor = new Color(1f, 1f, 1f, 1f);

    public override IEnumerator Execute(BossContext ctx)
    {
        Rect arena = ArenaBounds.PlayArea;
        bool haveArena = arena.width > 0f && arena.height > 0f;
        Vector2 center = haveArena ? (Vector2)arena.center
                       : ctx.Boss != null ? (Vector2)ctx.Boss.position : Vector2.zero;
        float w = haveArena ? arena.width : 30f;
        float h = haveArena ? arena.height : 18f;

        BossArea barH = ctx.SpawnArea(center);   // horizontal: ancho completo
        BossArea barV = ctx.SpawnArea(center);   // vertical: alto completo
        if (barH == null || barV == null)
        {
            if (barH != null) PoolManager.Despawn(barH);
            if (barV != null) PoolManager.Despawn(barV);
            yield return Recover();
            yield break;
        }

        Color fillC = WithAlpha(dodgeColor, alpha);
        barH.Configure(new Vector2(w, thickness), fillC, true, parryable);    // barrido por Y (sube)
        barV.Configure(new Vector2(thickness, h), fillC, false, parryable);   // barrido por X

        // (1) prep (telegraphSeconds heredado; 0 = relleno inmediato).
        if (telegraphSeconds > 0f) yield return new WaitForSeconds(telegraphSeconds);

        // (2) relleno simultáneo de ambas barras (mismo reloj).
        float t = 0f;
        while (t < fillSeconds)
        {
            float f = t / fillSeconds;
            barH.SetFill(f);
            barV.SetFill(f);
            t += Time.deltaTime;
            yield return null;
        }
        barH.SetFill(1f);
        barV.SetFill(1f);

        // (3) impacto: recolor + hitbox de ambas una ventana breve.
        Color impC = WithAlpha(impactColor, alpha);
        barH.SetColor(impC);
        barV.SetColor(impC);
        var info = new DamageInfo(damage, ctx.Boss != null ? ctx.Boss.gameObject : null, Vector2.zero) { parryable = parryable };
        barH.ActivateHitbox(info);
        barV.ActivateHitbox(info);
        if (impactSeconds > 0f) yield return new WaitForSeconds(impactSeconds);
        barH.DeactivateHitbox();
        barV.DeactivateHitbox();

        PoolManager.Despawn(barH);
        PoolManager.Despawn(barV);
        yield return Recover();
    }

    private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
