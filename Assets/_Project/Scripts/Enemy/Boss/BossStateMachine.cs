// Máquina de estados mínima del boss: guarda el estado activo y le reenvía los ticks.
// Espejo de PlayerStateMachine. Añadir nuevos estados (p. ej. una transición de fase con cinemática)
// es solo crear nuevas clases IBossState; esta clase no cambia.
public class BossStateMachine
{
    public IBossState Current { get; private set; }

    public void ChangeState(IBossState next)
    {
        Current?.Exit();
        Current = next;
        Current?.Enter();
    }

    public void Tick() => Current?.Tick();
    public void FixedTick() => Current?.FixedTick();
}
