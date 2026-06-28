// Estado final: el boss ha sido derrotado. Para los ataques y el movimiento y notifica la derrota
// (para la "coming soon card", parar música, etc.). Es un estado terminal: no transiciona a nada.
public class BossDefeatedState : IBossState
{
    private readonly BossController boss;

    public BossDefeatedState(BossController boss) => this.boss = boss;

    public void Enter()
    {
        boss.Director.Stop();
        boss.Movement?.SetBehavior(BossMoveBehavior.Hold);
        boss.NotifyDefeated();
    }

    public void Tick() { }
    public void FixedTick() { }
    public void Exit() { }
}
