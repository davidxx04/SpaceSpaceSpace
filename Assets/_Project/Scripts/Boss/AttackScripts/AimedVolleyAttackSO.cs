using System.Collections;
using UnityEngine;

// Ejemplo SEKIRO: tras el telegraph, lanza una ráfaga de balas DIRIGIDAS al jugador, una a una con
// separación temporal, para que el jugador encadene parries (parry -> parry -> parry...).
// Pon 'parryable = true' en el asset. Cada bala reapunta al jugador en el momento de disparar.
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Attacks/Aimed Volley", fileName = "AimedVolleyAttack")]
public class AimedVolleyAttackSO : BossAttackSO
{
    [Header("Disparo")]
    public float damage = 10f;
    public float projectileSpeed = 9f;
    public float range = 20f;

    [Header("Ráfaga")]
    [Min(1)] public int shots = 3;
    [Tooltip("Tiempo entre disparos de la ráfaga, en segundos (la 'ventana' para parrear cada uno).")]
    public float timeBetweenShots = 0.5f;

    [Tooltip("Si está marcado, ALTERNA bala parreable (cian) y no parreable (roja) en cada disparo de la " +
             "ráfaga -> fuerza esquiva/parry/esquiva/parry. (Ignora 'parryable' para el color de cada bala.)")]
    public bool alternateParry = false;

    public override IEnumerator Execute(BossContext ctx)
    {
        yield return Telegraph(ctx);

        for (int i = 0; i < shots; i++)
        {
            Vector2 origin = ctx.MuzzlePosition(0);
            Vector2 dir = ctx.AimToPlayer(origin);   // reapunta en cada disparo
            bool shotParryable = alternateParry ? (i % 2 == 1) : parryable;   // empieza por esquiva (roja)
            ctx.Spawn(origin, dir, projectileSpeed, range, false, damage, shotParryable);

            if (timeBetweenShots > 0f && i < shots - 1)
                yield return new WaitForSeconds(timeBetweenShots);
        }

        yield return Recover();
    }
}
