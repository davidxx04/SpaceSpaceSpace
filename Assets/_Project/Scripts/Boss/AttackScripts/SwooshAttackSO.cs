using System.Collections;
using UnityEngine;

// Ataque SWOOSH: una barra que BARRE en una dirección. Telegrafía con una línea fina (color de verbo),
// luego una barra cuerpo perpendicular que viaja a 'travelSpeed' por 'travelDistance' con el Hitbox
// activo. Por defecto NO parreable (esquiva saliéndote del arco); 'parryable' lo cambia (cian).
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Attacks/Swoosh", fileName = "SwooshAttack")]
public class SwooshAttackSO : BossAttackSO
{
    [Header("Daño / forma")]
    public float damage = 12f;

    [Tooltip("Ancho del barrido (perpendicular al avance), en unidades.")]
    public float width = 5f;

    [Tooltip("Grosor de la barra (a lo largo del avance), en unidades.")]
    public float thickness = 0.8f;

    [Header("Barrido")]
    public float travelSpeed = 14f;
    public float travelDistance = 12f;

    [Tooltip("Marcado: barre hacia el jugador (foto al lanzar). Desmarcado: usa 'fixedAngle'.")]
    public bool aimAtPlayer = true;

    [Tooltip("Ángulo fijo en grados si aimAtPlayer = false (0 = +X / derecha, 90 = arriba).")]
    public float fixedAngle = 90f;

    [Header("Telegrafía / color")]
    public float telegraphLength = 12f;

    [Tooltip("Color cuando NO es parreable (cálido = esquiva).")]
    public Color dodgeColor = new Color(1f, 0.3f, 0.1f, 0.9f);

    [Tooltip("Color cuando es parreable (frío = parry).")]
    public Color parryColor = new Color(0.2f, 0.8f, 1f, 0.9f);

    public override IEnumerator Execute(BossContext ctx)
    {
        Vector2 origin = ctx.MuzzlePosition(0);
        Vector2 dir = aimAtPlayer ? ctx.AimToPlayer(origin) : BossAttackUtils.AngleToDir(fixedAngle);
        Color c = parryable ? parryColor : dodgeColor;

        BossSwoosh sw = ctx.SpawnSwoosh(origin);
        if (sw == null) { yield return Recover(); yield break; }

        // (1) telegrafía: línea fina en la dirección del barrido.
        sw.ShowTelegraph(dir, telegraphLength, c);
        if (telegraphSeconds > 0f) yield return new WaitForSeconds(telegraphSeconds);

        // (2) barrido: la barra cuerpo viaja por 'travelDistance' a 'travelSpeed' con el hitbox activo.
        sw.BeginBody(dir, width, thickness, c);
        var info = new DamageInfo(damage, ctx.Boss != null ? ctx.Boss.gameObject : null, dir) { parryable = parryable };
        sw.ActivateHitbox(info);

        float traveled = 0f;
        while (traveled < travelDistance)
        {
            float step = travelSpeed * Time.deltaTime;
            sw.transform.position += (Vector3)(dir * step);
            traveled += step;
            yield return null;
        }

        sw.DeactivateHitbox();
        PoolManager.Despawn(sw);
        yield return Recover();
    }
}
