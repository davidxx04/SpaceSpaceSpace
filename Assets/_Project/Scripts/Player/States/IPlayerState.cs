// Un estado de la máquina de estados del jugador (andar/idle, rol y, más adelante,
// ataque/parry). Cada estado encapsula su propio comportamiento; las transiciones
// pasan por PlayerStateMachine.
public interface IPlayerState
{
    void Enter();
    void Tick();        // se llama desde Update
    void FixedTick();   // se llama desde FixedUpdate (física)
    void Exit();
}
