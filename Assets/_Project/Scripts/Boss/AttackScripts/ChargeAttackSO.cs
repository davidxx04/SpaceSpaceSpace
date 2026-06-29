using System.Collections;
using UnityEngine;

// EMBESTIDA (movimiento-como-ataque): el propio boss se lanza a toda velocidad hacia el jugador,
// lo SOBREPASA y frena con ease-out al otro lado. Hace daño por contacto si pilla al jugador en el
// trayecto. Se esquiva apartándose perpendicular a la línea de carga (por defecto NO parreable).
//
// El movimiento lo conduce BossMovement.Charge (un solo dueño del Rigidbody); el daño es un chequeo
// de contacto contra el IDamageable del jugador, una sola vez por embestida (la gracia post-golpe del
// jugador ya evita encadenar). Sin prefab ni hitbox de escena: autocontenido, va en todas las fases.
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Attacks/Charge", fileName = "ChargeAttack")]
public class ChargeAttackSO : BossAttackSO
{
    [Header("Daño / contacto")]
    public float damage = 14f;

    [Tooltip("Radio de impacto: si el jugador queda a esta distancia del boss durante la carga, recibe el golpe.")]
    public float hitRadius = 0.8f;

    [Header("Embestida")]
    [Tooltip("Velocidad de la carga, en unidades/segundo (debe ser MUY superior al andar del jugador).")]
    public float chargeSpeed = 22f;

    [Tooltip("Distancia extra que recorre tras sobrepasar al jugador mientras frena (ease-out).")]
    public float overshoot = 3f;

    public override IEnumerator Execute(BossContext ctx)
    {
        if (ctx.Movement == null)
        {
            Debug.LogWarning("[ChargeAttackSO] El boss no tiene BossMovement; no puede embestir.");
            yield return Recover();
            yield break;
        }

        // (1) telegraph: aviso previo (el boss se queda cargando antes de salir disparado).
        yield return Telegraph(ctx);

        // (2) fija rumbo AL FINAL del telegraph (re-apunta a la posición actual del jugador) y lanza.
        Vector2 origin = ctx.Boss.position;
        Vector2 dir = ctx.AimToPlayer(origin);
        float approach = ctx.Player != null ? Vector2.Distance(origin, ctx.Player.position) : 4f;
        ctx.Movement.Charge(dir, approach, overshoot, chargeSpeed);

        var info = new DamageInfo(damage, ctx.Boss != null ? ctx.Boss.gameObject : null, dir) { parryable = parryable };
        IDamageable target = ctx.Player != null ? ctx.Player.GetComponentInChildren<IDamageable>() : null;
        bool landed = false;

        // (3) mientras embiste: si el jugador entra en el radio de contacto, le pega UNA vez.
        float safety = (approach + overshoot) / chargeSpeed * 3f + 1f;   // por si algo bloquea la carga
        while (ctx.Movement.IsCharging && safety > 0f)
        {
            if (!landed && target != null && ctx.Player != null &&
                Vector2.Distance(ctx.Boss.position, ctx.Player.position) <= hitRadius)
            {
                target.TakeDamage(info);
                landed = true;
            }
            safety -= Time.deltaTime;
            yield return null;
        }

        yield return Recover();
    }
}
