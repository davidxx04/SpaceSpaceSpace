// Máquina de estados mínima: guarda el estado activo y le reenvía los ticks de
// actualización. Añadir ataque/parry más adelante es solo crear nuevas clases
// IPlayerState; esta clase no cambia.
public class PlayerStateMachine
{
    public IPlayerState Current { get; private set; }

    public void ChangeState(IPlayerState next)
    {
        Current?.Exit();
        Current = next;
        Current?.Enter();
    }

    public void Tick() => Current?.Tick();
    public void FixedTick() => Current?.FixedTick();
}
