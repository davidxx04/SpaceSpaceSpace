// Estado de combate: el boss ataca. Arranca el Director al entrar (que lanza ataques/combos según la
// fase) y lo para al salir (cubre cualquier salida a Stagger o Defeated). El movimiento lo lleva su
// propio componente; aquí solo aplicamos el comportamiento de la fase actual (también al volver de
// un Stagger).
public class BossCombatState : IBossState
{
    private readonly BossController boss;

    public BossCombatState(BossController boss) => this.boss = boss;

    public void Enter()
    {
        boss.ApplyPhaseMovement();
        boss.Director.Begin();
    }

    public void Exit()
    {
        boss.Director.Stop();
        boss.ClearSpawnedAttacks();   // limpia áreas/swooshes/balas en vuelo (sin esto quedan dibujados)
    }

    public void Tick() { }
    public void FixedTick() { }
}
