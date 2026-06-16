// Estado por defecto en el suelo: caminado lento en 8 direcciones (idle = input cero).
// El enrutado de los botones (rol/ataque) vive en PlayerController; este estado solo se
// ocupa del movimiento y declara que siempre se puede interrumpir.
public class LocomotionState : IPlayerState
{
    private readonly PlayerController player;

    public LocomotionState(PlayerController player)
    {
        this.player = player;
    }

    // Andando siempre se puede empezar cualquier acción.
    public bool CanInterrupt => true;

    public void Enter() { }
    public void Exit() { }
    public void Tick() { }

    public void FixedTick()
    {
        // Caminado lento: la velocidad sigue directamente al input.
        player.Rb.velocity = player.Input.MoveInput * player.WalkSpeed;
    }
}
