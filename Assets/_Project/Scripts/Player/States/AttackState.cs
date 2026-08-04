using UnityEngine;

// Ataque a DISTANCIA: en fireTime dispara projectileCount balas en abanico hacia el
// apuntado, aplica recoil y vuelve a locomoción. Sigue el patrón FSM + ScriptableObject.
public class AttackState : IPlayerState
{
    private readonly PlayerController player;

    private Vector2 direction;
    private float elapsed;
    private bool fired;

    public AttackState(PlayerController player)
    {
        this.player = player;
    }

    // Solo se puede cancelar el ataque en su tramo final (ventana de cancelación).
    public bool CanInterrupt =>
        player.CurrentAttackData.duration <= 0f ||
        elapsed >= player.CurrentAttackData.duration - player.CurrentAttackData.cancelWindow;

    public void Enter()
    {
        direction = player.AimDirection;   // dirección de disparo (fija al iniciar el ataque)
        elapsed = 0f;
        fired = false;
        player.NextAttackTime = Time.time + player.CurrentAttackData.cooldown;
    }

    public void Tick() { }

    public void FixedTick()
    {
        AttackData data = player.CurrentAttackData;
        elapsed += Time.fixedDeltaTime;

        // Movimiento parametrizable mientras disparas (0 = anclado, 1 = igual).
        player.Rb.velocity = player.Input.MoveInput * (player.WalkSpeed * data.moveMultiplier);

        float t = data.duration > 0f ? Mathf.Clamp01(elapsed / data.duration) : 1f;
        if (!fired && t >= data.fireTime)
        {
            Fire(data);
            fired = true;
        }

        if (elapsed >= data.duration)
        {
            player.StateMachine.ChangeState(player.LocomotionState);
        }
    }

    public void Exit() { }

    private void Fire(AttackData data)
    {
        AudioManager.PlayPlayerShoot();   // una vez por disparo (no por bala del abanico)

        if (data.projectilePrefab != null)
        {
            Vector2 spawnPos = player.Rb.position + direction * data.spawnOffset;
            float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            int count = Mathf.Max(1, data.projectileCount);
            float startAngle = baseAngle - data.spreadAngle * 0.5f;
            float stepAngle = count > 1 ? data.spreadAngle / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angle = count > 1 ? startAngle + stepAngle * i : baseAngle;
                float rad = angle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                // Nace ya orientada (no identidad) para que no haya ni un frame de interpolación torcida.
                Projectile proj = PoolManager.Spawn(data.projectilePrefab, spawnPos, Quaternion.Euler(0f, 0f, angle));
                if (proj == null) continue;
                var info = new DamageInfo(data.damage, player.gameObject, dir);   // parryable=false (bala del jugador)
                proj.Launch(dir, data.projectileSpeed, data.range, data.pierce, info);
            }

            if (data.vfxPrefab != null)
            {
                Object.Instantiate(data.vfxPrefab, spawnPos, Quaternion.Euler(0f, 0f, baseAngle));
            }
        }

        // Recoil hacia atrás (opuesto al disparo).
        player.ApplyRecoil(-direction, data);

        // Cobra la energía del arma resuelta (no hace nada si el disparo fue el normal).
        player.NotifyAttackFired();
    }
}
