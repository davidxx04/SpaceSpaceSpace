using System.Collections;
using UnityEngine;

// Ataque ANILLO/ESPIRAL: tras el telegraph, dispara uno o varios anillos de balas repartidas en 360°
// (BossAttackUtils.Radial). Con 'spinPerRing' ≠ 0 los anillos sucesivos rotan -> espiral. Cubre los
// arquetipos "anillo radial" y "espiral" en un solo tipo. Normalmente NO parreable (esquiva).
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Attacks/Radial Burst", fileName = "RadialBurstAttack")]
public class RadialBurstAttackSO : BossAttackSO
{
    [Header("Disparo")]
    public float damage = 10f;
    public float projectileSpeed = 6f;
    public float range = 20f;
    public bool pierce = false;

    [Header("Anillo")]
    [Min(1)] public int count = 14;
    [Min(1)] public int rings = 1;

    [Tooltip("Tiempo entre anillos sucesivos, en segundos.")]
    public float timeBetweenRings = 0.25f;

    [Tooltip("Offset angular añadido a cada anillo (≠0 = espiral), en grados.")]
    public float spinPerRing = 0f;

    [Tooltip("Offset angular inicial del primer anillo, en grados.")]
    public float startAngle = 0f;

    public override IEnumerator Execute(BossContext ctx)
    {
        yield return Telegraph(ctx);

        for (int r = 0; r < rings; r++)
        {
            Vector2 origin = ctx.MuzzlePosition(0);
            float startDeg = startAngle + spinPerRing * r;
            foreach (Vector2 dir in BossAttackUtils.Radial(count, startDeg))
                ctx.Spawn(origin, dir, projectileSpeed, range, pierce, damage, parryable);

            if (timeBetweenRings > 0f && r < rings - 1)
                yield return new WaitForSeconds(timeBetweenRings);
        }

        yield return Recover();
    }
}
