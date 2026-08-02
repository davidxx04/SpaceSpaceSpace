using UnityEngine;

// Parry: estado defensivo y REACTIVO. No daña; abre una ventana durante la cual
// PlayerController.IsParrying = true. La intercepción real del ataque enemigo ocurre en
// PlayerDamageReceiver cuando un golpe parreable impacta dentro de esa ventana.
public class ParryState : IPlayerState
{
    private readonly PlayerController player;
    private float elapsed;

    // ¿Esta activación de parry ya acertó? Habilita la cancelación inmediata y sirve de dedup
    // (1 parry = 1 acierto contable, aunque desvíe varios golpes en la misma ventana).
    private bool landed;

    public ParryState(PlayerController player)
    {
        this.player = player;
    }

    // Se puede cancelar el parry: al ACERTAR (si cancelOnSuccess, abre la cancel window al instante)
    // o en su tramo final (ventana de cancelación normal).
    public bool CanInterrupt =>
        (landed && player.ParryData.cancelOnSuccess) ||
        player.ParryData.duration <= 0f ||
        elapsed >= player.ParryData.duration - player.ParryData.cancelWindow;

    public void Enter()
    {
        ParryData data = player.ParryData;
        elapsed = 0f;
        landed = false;
        player.NextParryTime = Time.time + data.cooldown;
        player.BeginShipSpin();

        if (data.vfxPrefab != null)
        {
            Object.Instantiate(data.vfxPrefab, player.Rb.position, Quaternion.identity);
        }
    }

    // Lo llama PlayerController cuando PlayerDamageReceiver confirma un parry. Marca el acierto:
    // habilita la cancelación inmediata (como entrar en la cancel window) y cancela el cooldown del
    // parry para poder re-parrear al instante. Devuelve true solo la PRIMERA vez de esta activación
    // (1 parry = 1 acierto contable; útil para la futura energía del ataque especial).
    public bool RegisterSuccess()
    {
        bool firstThisActivation = !landed;
        landed = true;
        if (firstThisActivation && player.ParryData.cancelOnSuccess)
            player.NextParryTime = Time.time;
        return firstThisActivation;
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

        // Progreso 0..1 dentro de la ventana, para que el aura encoja el anillo de timing.
        player.ParryWindowProgress = Mathf.InverseLerp(data.parryWindowStart, data.parryWindowEnd, t);

        if (elapsed >= data.duration)
        {
            player.IsParrying = false;
            player.StateMachine.ChangeState(player.LocomotionState);
        }
    }

    public void Exit()
    {
        player.IsParrying = false;
        player.EndShipSpin();
    }
}
