using System.Collections;
using UnityEngine;

// Ejemplo BULLET-HELL: tras el telegraph, dispara un abanico de balas NO parreables hacia el jugador.
// Se esquiva con los dashes. Sirve también de plantilla para ataques de patrón de balas.
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Attacks/Spread Shot", fileName = "SpreadShotAttack")]
public class SpreadShotAttackSO : BossAttackSO
{
    [Header("Disparo")]
    public float damage = 10f;
    public float projectileSpeed = 8f;
    public float range = 20f;
    public bool pierce = false;

    [Header("Abanico")]
    [Min(1)] public int projectileCount = 5;
    [Tooltip("Apertura total del abanico, en grados.")]
    public float spreadAngle = 40f;

    public override IEnumerator Execute(BossContext ctx)
    {
        yield return Telegraph(ctx);

        Vector2 origin = ctx.MuzzlePosition(0);
        Vector2 baseDir = ctx.AimToPlayer(origin);

        foreach (Vector2 dir in BossAttackUtils.Fan(baseDir, projectileCount, spreadAngle))
        {
            ctx.Spawn(origin, dir, projectileSpeed, range, pierce, damage, parryable);
        }

        yield return Recover();
    }
}
