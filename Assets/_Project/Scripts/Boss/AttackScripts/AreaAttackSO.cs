using System.Collections;
using UnityEngine;

// Ataque de ÁREA (§10.2/§12): zona rectangular telegrafiada por barra de relleno.
// prep (telegraphSeconds = retardo antes del relleno; 0 = inmediato) -> relleno (fillSeconds = el reloj)
// -> impacto (impactSeconds, con su propio color) -> recovery.
// Color del relleno por verbo ('parryable': rojo=esquiva / cian=parry); daño SOLO en el impacto.
// Modos de colocación (precedencia): fromBossToPlayer > fillScreenX/Y > normal (atPlayer/trackPlayer).
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Attacks/Area", fileName = "AreaAttack")]
public class AreaAttackSO : BossAttackSO
{
    [Header("Forma / colocación")]
    [Tooltip("Tamaño del rectángulo (ancho, alto) en unidades.")]
    public Vector2 size = new Vector2(4f, 4f);

    [Tooltip("Marcado: el área aparece sobre el jugador (foto al lanzar). Desmarcado: sobre el boss.")]
    public bool atPlayer = true;

    [Tooltip("Marcado: el área SIGUE al jugador durante la telegrafía y se fija 'lockSeconds' antes del impacto.")]
    public bool trackPlayer = false;

    [Tooltip("Cuánto ANTES del impacto el área deja de seguir al jugador y se queda quieta (solo si trackPlayer). Mantenlo < fillSeconds.")]
    public float lockSeconds = 0.2f;

    [Tooltip("Desplazamiento extra respecto al origen elegido (jugador o boss).")]
    public Vector2 offset = Vector2.zero;

    [Tooltip("El relleno crece por el eje Y (marcado) o por el eje X (desmarcado).")]
    public bool fillAxisY = true;

    [Tooltip("Traza el área DESDE EL BOSS hacia el jugador, con longitud auto hasta el borde de la arena " +
             "(rota el área e ignora size.x). Combínalo con trackPlayer para un rayo que persigue.")]
    public bool fromBossToPlayer = false;

    [Tooltip("Estira el área a TODO EL ANCHO de la arena (eje X) y la centra ahí (crowd control).")]
    public bool fillScreenX = false;

    [Tooltip("Estira el área a TODO EL ALTO de la arena (eje Y) y la centra ahí (crowd control).")]
    public bool fillScreenY = false;

    [Header("Tiempos")]
    [Tooltip("Duración del relleno = telegrafía real (marca cuándo cae el golpe), en segundos.")]
    public float fillSeconds = 0.7f;

    [Tooltip("Cuánto permanece activo el golpe tras llenarse, en segundos.")]
    public float impactSeconds = 0.1f;

    [Header("Daño / color")]
    public float damage = 10f;

    [Range(0f, 1f)]
    [Tooltip("Opacidad del relleno y del impacto (estético). El glow vive en BossArea.")]
    public float alpha = 0.55f;

    [Tooltip("Color de relleno cuando NO es parreable (cálido = esquiva).")]
    public Color dodgeColor = new Color(1f, 0.35f, 0.1f, 1f);

    [Tooltip("Color de relleno cuando es parreable (frío = parry).")]
    public Color parryColor = new Color(0.2f, 0.8f, 1f, 1f);

    [Tooltip("Color del flash de IMPACTO (mientras pega), común a parreable y no parreable.")]
    public Color impactColor = new Color(1f, 1f, 1f, 1f);

    public override IEnumerator Execute(BossContext ctx)
    {
        Rect arena = ArenaBounds.PlayArea;

        ComputePlacement(ctx, arena, out Vector2 pos, out Quaternion rot, out Vector2 finalSize, out bool fillVertical);

        BossArea area = ctx.SpawnArea(pos);
        if (area == null) { yield return Recover(); yield break; }

        area.Configure(finalSize, WithAlpha(parryable ? parryColor : dodgeColor, alpha), fillVertical, parryable);
        area.transform.rotation = rot;

        // (1) prep neutro. telegraphSeconds = retardo antes de empezar el relleno (0 = inmediato).
        float prep = telegraphSeconds;
        while (prep > 0f)
        {
            if (trackPlayer) UpdatePlacement(ctx, arena, area, prep + fillSeconds);
            prep -= Time.deltaTime;
            yield return null;
        }

        // (2) relleno: el progreso 0->1 ES la telegrafía. El seguimiento se detiene (el área se "fija")
        // cuando faltan 'lockSeconds' para el impacto.
        float t = 0f;
        while (t < fillSeconds)
        {
            if (trackPlayer) UpdatePlacement(ctx, arena, area, fillSeconds - t);
            area.SetFill(t / fillSeconds);
            t += Time.deltaTime;
            yield return null;
        }
        area.SetFill(1f);

        // (3) impacto: recolorea al flash de impacto y activa el hitbox una ventana breve.
        area.SetColor(WithAlpha(impactColor, alpha));
        var info = new DamageInfo(damage, ctx.Boss != null ? ctx.Boss.gameObject : null, Vector2.zero) { parryable = parryable };
        area.ActivateHitbox(info);
        if (impactSeconds > 0f) yield return new WaitForSeconds(impactSeconds);
        area.DeactivateHitbox();

        PoolManager.Despawn(area);
        yield return Recover();
    }

    // Reposiciona/redimensiona el área siguiendo al jugador, hasta que falten <= lockSeconds para el impacto.
    private void UpdatePlacement(BossContext ctx, Rect arena, BossArea area, float secondsUntilImpact)
    {
        if (ctx.Player == null || secondsUntilImpact <= lockSeconds) return;
        ComputePlacement(ctx, arena, out Vector2 pos, out Quaternion rot, out Vector2 finalSize, out _);
        area.transform.position = pos;
        area.transform.rotation = rot;
        if (fromBossToPlayer) area.SetSize(finalSize);   // el rayo cambia de longitud al moverse el jugador
    }

    // Calcula posición/rotación/tamaño/eje-de-relleno según los modos activos.
    private void ComputePlacement(BossContext ctx, Rect arena, out Vector2 pos, out Quaternion rot, out Vector2 finalSize, out bool fillVertical)
    {
        Vector2 playerPos = ctx.Player != null ? (Vector2)ctx.Player.position : Vector2.zero;
        Vector2 bossPos = ctx.Boss != null ? (Vector2)ctx.Boss.position : Vector2.zero;

        // Modo rayo: sale del boss hacia el jugador y llega al borde de la arena; rota el área. Ignora size.x.
        if (fromBossToPlayer)
        {
            Vector2 dir = playerPos - bossPos;
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
            float length = RayToArenaEdge(bossPos, dir, arena);
            finalSize = new Vector2(length, size.y);
            pos = bossPos + dir * (length * 0.5f) + offset;
            rot = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            fillVertical = false;   // el relleno sale del extremo del boss a lo largo del rayo (eje X local)
            return;
        }

        // Modo normal (con override de pantalla completa por eje).
        finalSize = size;
        Vector2 basePos = (trackPlayer || atPlayer) ? playerPos : bossPos;
        pos = basePos + offset;

        if (fillScreenX && arena.width > 0f) { finalSize.x = arena.width; pos.x = arena.center.x; }
        if (fillScreenY && arena.height > 0f) { finalSize.y = arena.height; pos.y = arena.center.y; }

        rot = Quaternion.identity;
        fillVertical = fillAxisY;
    }

    // Distancia desde 'origin' (dentro de la arena) a lo largo de 'dir' hasta el borde del rectángulo.
    private static float RayToArenaEdge(Vector2 origin, Vector2 dir, Rect arena)
    {
        if (arena.width <= 0f || arena.height <= 0f) return 30f;   // sin ArenaBounds: longitud por defecto

        float tMax = float.PositiveInfinity;
        if (Mathf.Abs(dir.x) > 1e-5f)
        {
            float tx = ((dir.x > 0f ? arena.xMax : arena.xMin) - origin.x) / dir.x;
            if (tx > 0f) tMax = Mathf.Min(tMax, tx);
        }
        if (Mathf.Abs(dir.y) > 1e-5f)
        {
            float ty = ((dir.y > 0f ? arena.yMax : arena.yMin) - origin.y) / dir.y;
            if (ty > 0f) tMax = Mathf.Min(tMax, ty);
        }
        if (float.IsInfinity(tMax) || tMax <= 0f) tMax = Mathf.Max(arena.width, arena.height);
        return tMax;
    }

    private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
