using System.Collections;
using UnityEngine;

// Ataque DOBLE LÍNEA: "NO te muevas". Aimed — al empezar fija en MUNDO el centro (= posición del jugador)
// y dos dianas a ±gapDist perpendicular a la dirección boss→jugador. Cada disparo lanza una bala hacia
// cada diana, así dos hileras pasan a ambos lados del jugador dejando un pasillo seguro DONDE ESTABA:
// quedarse quieto = a salvo; moverse de lado = comerse una línea. No parreable.
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Attacks/Double Line", fileName = "DoubleLineAttack")]
public class DoubleLineAttackSO : BossAttackSO
{
    [Header("Disparo")]
    public float damage = 8f;
    public float projectileSpeed = 9f;
    public float range = 22f;

    [Header("Ráfaga / pasillo")]
    [Min(1)] public int shots = 12;

    [Tooltip("Tiempo entre disparos de cada hilera, en segundos.")]
    public float timeBetweenShots = 0.07f;

    [Tooltip("Medio ancho del pasillo seguro (separación de cada línea respecto al centro), en unidades. " +
             "Menor = más estrecho (más exige no moverse).")]
    public float gapDist = 0.9f;

    public override IEnumerator Execute(BossContext ctx)
    {
        yield return Telegraph(ctx);

        // Centro y dianas FIJOS en mundo, calculados al inicio (el pasillo queda anclado a donde estaba el jugador).
        Vector2 origin = ctx.MuzzlePosition(0);
        Vector2 center = ctx.Player != null ? (Vector2)ctx.Player.position : origin + Vector2.up;
        Vector2 aim = center - origin;
        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.up;
        Vector2 perp = new Vector2(-aim.y, aim.x);   // lateral (perpendicular a la dir boss→jugador)
        Vector2 targetA = center + perp * gapDist;
        Vector2 targetB = center - perp * gapDist;

        for (int i = 0; i < shots; i++)
        {
            Vector2 o = ctx.MuzzlePosition(0);   // muzzle actual: robusto aunque el boss se mueva
            EmitToward(ctx, o, targetA);
            EmitToward(ctx, o, targetB);
            if (timeBetweenShots > 0f && i < shots - 1)
                yield return new WaitForSeconds(timeBetweenShots);
        }

        yield return Recover();
    }

    private void EmitToward(BossContext ctx, Vector2 origin, Vector2 target)
    {
        Vector2 dir = target - origin;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
        ctx.Spawn(origin, dir, projectileSpeed, range, false, damage, parryable);
    }
}
