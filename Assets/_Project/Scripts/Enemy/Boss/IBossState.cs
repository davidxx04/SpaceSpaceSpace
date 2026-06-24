// Un estado de la máquina de estados de alto nivel del boss (Intro / Combat / Stagger / Defeated).
// A diferencia del jugador, las transiciones del boss las decide su propia lógica (no el input),
// por eso aquí NO hay 'CanInterrupt': basta con Enter/Tick/FixedTick/Exit.
public interface IBossState
{
    void Enter();
    void Tick();        // se llama desde Update
    void FixedTick();   // se llama desde FixedUpdate (física)
    void Exit();
}
