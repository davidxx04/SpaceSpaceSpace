using UnityEngine;

// Estado de aturdimiento: tras romper la postura a base de parries, el boss queda vulnerable y deja
// de atacar durante 'staggerSeconds' (ventana de castigo para que el jugador pegue al core). Luego
// vuelve a combate. Marca IsStaggered para que los VFX puedan reaccionar.
public class BossStaggerState : IBossState
{
    private readonly BossController boss;
    private float endTime;

    public BossStaggerState(BossController boss) => this.boss = boss;

    public void Enter()
    {
        boss.ResetPosture();
        boss.IsStaggered = true;
        boss.Movement?.SetBehavior(BossMoveBehavior.Hold);
        endTime = Time.time + boss.StaggerSeconds;
    }

    public void Tick()
    {
        if (Time.time >= endTime)
            boss.StateMachine.ChangeState(boss.CombatState);
    }

    public void Exit()
    {
        boss.IsStaggered = false;
    }

    public void FixedTick() { }
}
