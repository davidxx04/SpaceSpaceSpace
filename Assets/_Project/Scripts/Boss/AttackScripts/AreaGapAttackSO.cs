using System.Collections;
using UnityEngine;

// Ataque de ÁREA CON HUECO: cubre TODO el rectángulo del arena excepto una única franja libre
// (horizontal o vertical), a diferencia de CrossAreaAttackSO (que deja 4 huecos, uno por esquina).
// Reutiliza dos BossArea (pooleadas) como en CrossAreaAttackSO, pero dimensionadas de forma asimétrica
// para dejar solo el carril libre. La franja SIGUE al jugador durante la telegrafía y se congela
// 'lockSeconds' antes del impacto (mismo patrón que AreaAttackSO.trackPlayer), así el hueco siempre es
// alcanzable. 'gapWidth' está acotado deliberadamente estrecho: nunca debe ser un pasillo cómodo.
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Attacks/Area Gap", fileName = "AreaGapAttack")]
public class AreaGapAttackSO : BossAttackSO
{
    public enum GapOrientation { Horizontal, Vertical, Random }

    [Header("Forma / colocación")]
    [Tooltip("Orientación de la franja libre. 'Random' sortea horizontal o vertical al lanzar.")]
    public GapOrientation orientation = GapOrientation.Random;

    [Range(1.5f, 3.5f)]
    [Tooltip("Ancho de la franja libre, en unidades. Acotado a propósito: debe seguir siendo un hueco " +
             "pequeño, nunca un pasillo cómodo.")]
    public float gapWidth = 2.5f;

    [Tooltip("Marcado: la franja SIGUE la posición del jugador durante la telegrafía y se fija " +
             "'lockSeconds' antes del impacto (igual que AreaAttackSO.trackPlayer), para que el hueco " +
             "siempre sea alcanzable. Desmarcado: la franja queda fija en el centro de la arena.")]
    public bool trackPlayer = true;

    [Tooltip("Cuánto ANTES del impacto la franja deja de seguir al jugador y se queda quieta (solo si trackPlayer).")]
    public float lockSeconds = 0.2f;

    [Header("Tiempos")]
    [Tooltip("Duración del relleno = telegrafía real (marca cuándo cae el golpe), en segundos.")]
    public float fillSeconds = 0.7f;

    [Tooltip("Cuánto permanece activo el golpe tras llenarse, en segundos.")]
    public float impactSeconds = 0.12f;

    [Header("Daño / color")]
    public float damage = 10f;

    [Range(0f, 1f)]
    public float alpha = 0.6f;

    [Tooltip("Color de relleno cuando NO es parreable (cálido = esquiva).")]
    public Color dodgeColor = new Color(1f, 0.32f, 0.08f, 1f);

    [Tooltip("Color de relleno cuando es parreable (frío = parry).")]
    public Color parryColor = new Color(0.2f, 0.8f, 1f, 1f);

    [Tooltip("Color del flash de impacto, común a parreable y no parreable.")]
    public Color impactColor = new Color(1f, 1f, 1f, 1f);

    public override IEnumerator Execute(BossContext ctx)
    {
        Rect arena = ArenaBounds.PlayArea;
        if (arena.width <= 0f || arena.height <= 0f) { yield return Recover(); yield break; }

        bool gapIsVertical = ResolveOrientation();
        bool barsFillY = !gapIsVertical;   // franja horizontal libre -> las barras rellenan por Y (y viceversa)

        ComputeBars(ctx, arena, gapIsVertical, out Vector2 posA, out Vector2 sizeA, out Vector2 posB, out Vector2 sizeB);

        BossArea barA = ctx.SpawnArea(posA);
        BossArea barB = ctx.SpawnArea(posB);
        if (barA == null || barB == null)
        {
            if (barA != null) PoolManager.Despawn(barA);
            if (barB != null) PoolManager.Despawn(barB);
            yield return Recover();
            yield break;
        }

        Color fillC = WithAlpha(parryable ? parryColor : dodgeColor, alpha);
        barA.Configure(sizeA, fillC, barsFillY, parryable);
        barB.Configure(sizeB, fillC, barsFillY, parryable);

        // (1) prep: telegraphSeconds heredado (0 = relleno inmediato), con seguimiento si trackPlayer.
        float prep = telegraphSeconds;
        while (prep > 0f)
        {
            if (trackPlayer) UpdateBars(ctx, arena, gapIsVertical, barA, barB, prep + fillSeconds);
            prep -= Time.deltaTime;
            yield return null;
        }

        // (2) relleno = telegrafía real; se congela cuando faltan 'lockSeconds' para el impacto.
        float t = 0f;
        while (t < fillSeconds)
        {
            if (trackPlayer) UpdateBars(ctx, arena, gapIsVertical, barA, barB, fillSeconds - t);
            float f = t / fillSeconds;
            barA.SetFill(f);
            barB.SetFill(f);
            t += Time.deltaTime;
            yield return null;
        }
        barA.SetFill(1f);
        barB.SetFill(1f);

        // (3) impacto: recolor + hitbox de ambas barras una ventana breve.
        Color impC = WithAlpha(impactColor, alpha);
        barA.SetColor(impC);
        barB.SetColor(impC);
        var info = new DamageInfo(damage, ctx.Boss != null ? ctx.Boss.gameObject : null, Vector2.zero) { parryable = parryable };
        barA.ActivateHitbox(info);
        barB.ActivateHitbox(info);
        if (impactSeconds > 0f) yield return new WaitForSeconds(impactSeconds);
        barA.DeactivateHitbox();
        barB.DeactivateHitbox();

        PoolManager.Despawn(barA);
        PoolManager.Despawn(barB);
        yield return Recover();
    }

    // Reposiciona/redimensiona ambas barras siguiendo al jugador, hasta que falten <= lockSeconds.
    private void UpdateBars(BossContext ctx, Rect arena, bool gapIsVertical, BossArea barA, BossArea barB, float secondsUntilImpact)
    {
        if (ctx.Player == null || secondsUntilImpact <= lockSeconds) return;
        ComputeBars(ctx, arena, gapIsVertical, out Vector2 posA, out Vector2 sizeA, out Vector2 posB, out Vector2 sizeB);
        barA.transform.position = posA;
        barA.SetSize(sizeA);
        barB.transform.position = posB;
        barB.SetSize(sizeB);
    }

    private bool ResolveOrientation()
    {
        switch (orientation)
        {
            case GapOrientation.Horizontal: return false;
            case GapOrientation.Vertical: return true;
            default: return Random.value < 0.5f;   // Random: se sortea UNA vez al lanzar
        }
    }

    // Calcula posición/tamaño de las 2 barras que, juntas, cubren el rectángulo del arena entero
    // salvo una franja de 'gapWidth' centrada en la coordenada actual del jugador (clampada para que
    // el hueco quede siempre dentro del arena).
    private void ComputeBars(BossContext ctx, Rect arena, bool gapIsVertical, out Vector2 posA, out Vector2 sizeA, out Vector2 posB, out Vector2 sizeB)
    {
        Vector2 playerPos = ctx.Player != null ? (Vector2)ctx.Player.position : arena.center;
        float half = gapWidth * 0.5f;

        if (!gapIsVertical)   // franja HORIZONTAL libre -> barras arriba/abajo, ancho completo cada una
        {
            float gapCenterY = Mathf.Clamp(playerPos.y, arena.yMin + half, arena.yMax - half);
            float bottomH = Mathf.Max(0.01f, (gapCenterY - half) - arena.yMin);
            float topH = Mathf.Max(0.01f, arena.yMax - (gapCenterY + half));

            sizeA = new Vector2(arena.width, bottomH);
            posA = new Vector2(arena.center.x, arena.yMin + bottomH * 0.5f);
            sizeB = new Vector2(arena.width, topH);
            posB = new Vector2(arena.center.x, arena.yMax - topH * 0.5f);
        }
        else                  // franja VERTICAL libre -> barras izquierda/derecha, alto completo cada una
        {
            float gapCenterX = Mathf.Clamp(playerPos.x, arena.xMin + half, arena.xMax - half);
            float leftW = Mathf.Max(0.01f, (gapCenterX - half) - arena.xMin);
            float rightW = Mathf.Max(0.01f, arena.xMax - (gapCenterX + half));

            sizeA = new Vector2(leftW, arena.height);
            posA = new Vector2(arena.xMin + leftW * 0.5f, arena.center.y);
            sizeB = new Vector2(rightW, arena.height);
            posB = new Vector2(arena.xMax - rightW * 0.5f, arena.center.y);
        }
    }

    private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
