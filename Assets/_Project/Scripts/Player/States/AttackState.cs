using UnityEngine;

// Ataque básico DIRECCIONAL: golpea en la dirección de apuntado activando una hitbox
// de trigger durante una ventana configurable. La movilidad durante el ataque es
// parametrizable (moveMultiplier). Sigue el patrón FSM + ScriptableObject.
public class AttackState : IPlayerState
{
    private readonly PlayerController player;

    private Vector2 direction;
    private float elapsed;
    private bool hitboxActive;

    public AttackState(PlayerController player)
    {
        this.player = player;
    }

    // Solo se puede cancelar el ataque en su tramo final (ventana de cancelación).
    public bool CanInterrupt =>
        player.AttackData.duration <= 0f ||
        elapsed >= player.AttackData.duration - player.AttackData.cancelWindow;

    public void Enter()
    {
        AttackData data = player.AttackData;

        direction = player.AimDirection;   // dirección de apuntado en el momento del golpe
        elapsed = 0f;
        hitboxActive = false;
        player.NextAttackTime = Time.time + data.cooldown;

        PlaceHitbox(data);
        player.AttackHitbox.SetDebug(data.showDebugHitbox);

        if (data.vfxPrefab != null)
        {
            Vector2 pos = player.Rb.position + direction * data.hitboxDistance;
            Object.Instantiate(data.vfxPrefab, pos, Quaternion.identity);
        }
    }

    public void Tick() { }

    public void FixedTick()
    {
        AttackData data = player.AttackData;
        elapsed += Time.fixedDeltaTime;

        // Movimiento parametrizable durante el ataque (0 = anclado).
        player.Rb.velocity = player.Input.MoveInput * (player.WalkSpeed * data.moveMultiplier);

        float t = data.duration > 0f ? Mathf.Clamp01(elapsed / data.duration) : 1f;

        // Enciende/apaga la hitbox según la ventana de golpe configurada.
        bool shouldBeActive = t >= data.hitStart && t <= data.hitEnd;
        if (shouldBeActive && !hitboxActive)
        {
            var info = new DamageInfo(data.damage, player.gameObject, direction);
            player.AttackHitbox.Activate(info);
            hitboxActive = true;
        }
        else if (!shouldBeActive && hitboxActive)
        {
            player.AttackHitbox.Deactivate();
            hitboxActive = false;
        }

        if (elapsed >= data.duration)
        {
            player.StateMachine.ChangeState(player.LocomotionState);
        }
    }

    public void Exit()
    {
        player.AttackHitbox.Deactivate();
        hitboxActive = false;
    }

    private void PlaceHitbox(AttackData data)
    {
        Transform t = player.AttackHitbox.transform;
        t.localPosition = direction * data.hitboxDistance;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        t.localRotation = Quaternion.Euler(0f, 0f, angle);

        player.AttackHitbox.SetBoxSize(data.hitboxSize);
    }
}
