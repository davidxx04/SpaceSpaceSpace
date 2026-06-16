using UnityEngine;

// Parry: estado defensivo y REACTIVO. No daña; abre una ventana durante la cual
// PlayerController.IsParrying = true. La intercepción real del ataque enemigo ocurre en
// PlayerDamageReceiver cuando un golpe parreable impacta dentro de esa ventana.
public class ParryState : IPlayerState
{
    private readonly PlayerController player;
    private float elapsed;

    public ParryState(PlayerController player)
    {
        this.player = player;
    }

    // Solo se puede cancelar el parry en su tramo final (ventana de cancelación).
    public bool CanInterrupt =>
        player.ParryData.duration <= 0f ||
        elapsed >= player.ParryData.duration - player.ParryData.cancelWindow;

    public void Enter()
    {
        ParryData data = player.ParryData;
        elapsed = 0f;
        player.NextParryTime = Time.time + data.cooldown;

        if (data.vfxPrefab != null)
        {
            Object.Instantiate(data.vfxPrefab, player.Rb.position, Quaternion.identity);
        }
    }

    public void Tick() { }

    public void FixedTick()
    {
        ParryData data = player.ParryData;
        elapsed += Time.fixedDeltaTime;

        // Movimiento parametrizable durante el parry (0 = anclado).
        player.Rb.velocity = player.Input.MoveInput * (player.WalkSpeed * data.moveMultiplier);

        // IsParrying activo solo dentro de la ventana configurada.
        float t = data.duration > 0f ? Mathf.Clamp01(elapsed / data.duration) : 1f;
        player.IsParrying = t >= data.parryWindowStart && t <= data.parryWindowEnd;

        if (elapsed >= data.duration)
        {
            player.IsParrying = false;
            player.StateMachine.ChangeState(player.LocomotionState);
        }
    }

    public void Exit()
    {
        player.IsParrying = false;
    }
}
