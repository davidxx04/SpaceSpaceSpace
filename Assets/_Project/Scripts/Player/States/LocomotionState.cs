// Estado por defecto en el suelo: caminado lento en 8 direcciones (idle = input cero).
// Escucha el botón de rol para transicionar a RollState.
public class LocomotionState : IPlayerState
{
    private readonly PlayerController player;

    public LocomotionState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        player.Input.RollPerformed += OnRollPressed;
    }

    public void Exit()
    {
        player.Input.RollPerformed -= OnRollPressed;
    }

    public void Tick() { }

    public void FixedTick()
    {
        // Caminado lento: la velocidad sigue directamente al input.
        player.Rb.velocity = player.Input.MoveInput * player.WalkSpeed;
    }

    private void OnRollPressed()
    {
        if (player.CanRoll)
        {
            player.StateMachine.ChangeState(player.RollState);
        }
    }
}
