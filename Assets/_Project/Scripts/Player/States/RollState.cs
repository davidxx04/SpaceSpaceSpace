using UnityEngine;

// Esquiva/deslizamiento: recorre una distancia fija en un tiempo fijo siguiendo una
// AnimationCurve (rápido -> lento). Concede invulnerabilidad durante la ventana de
// i-frames configurada y, al terminar, vuelve a locomoción e inicia el cooldown.
public class RollState : IPlayerState
{
    private readonly PlayerController player;

    private Vector2 startPos;
    private Vector2 direction;
    private float elapsed;

    public RollState(PlayerController player)
    {
        this.player = player;
    }

    // Solo se puede cancelar el rol en su tramo final (ventana de cancelación).
    public bool CanInterrupt =>
        player.RollData.duration <= 0f ||
        elapsed >= player.RollData.duration - player.RollData.cancelWindow;

    public void Enter()
    {
        RollData data = player.RollData;

        direction = player.AimDirection;   // dirección de apuntado en el momento del rol
        startPos = player.Rb.position;
        elapsed = 0f;

        player.Rb.velocity = Vector2.zero;             // el rol se mueve por MovePosition, no por velocidad
        player.NextRollTime = Time.time + data.cooldown;

        if (data.vfxPrefab != null)
        {
            Object.Instantiate(data.vfxPrefab, startPos, Quaternion.identity);
        }

        // Estela del dash: se emite durante todo el rol y se detiene al salir.
        if (player.Afterimage != null && data.afterimage != null)
        {
            player.Afterimage.StartEmitting(data.afterimage);
        }
    }

    public void Tick() { }

    public void FixedTick()
    {
        RollData data = player.RollData;
        elapsed += Time.fixedDeltaTime;

        float t = data.duration > 0f ? Mathf.Clamp01(elapsed / data.duration) : 1f;

        // I-frames activos solo dentro de la ventana configurada.
        player.IsInvulnerable = t >= data.iFrameStart && t <= data.iFrameEnd;

        // La curva reparte la distancia en el tiempo (de ahí el efecto rápido->lento).
        float progress = data.movementCurve.Evaluate(t);
        Vector2 target = startPos + direction * (data.distance * progress);
        player.Rb.MovePosition(target);

        if (elapsed >= data.duration)
        {
            player.IsInvulnerable = false;
            player.StateMachine.ChangeState(player.LocomotionState);
        }
    }

    public void Exit()
    {
        player.IsInvulnerable = false;
        if (player.Afterimage != null) player.Afterimage.StopEmitting();
    }
}
