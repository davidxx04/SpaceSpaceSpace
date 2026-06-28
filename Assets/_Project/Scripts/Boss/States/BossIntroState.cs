using UnityEngine;

// Estado de entrada del boss: espera 'introSeconds' (sitio para una animación/cinemática futura) y
// luego pasa a combate. El boss permanece quieto.
public class BossIntroState : IBossState
{
    private readonly BossController boss;
    private float endTime;

    public BossIntroState(BossController boss) => this.boss = boss;

    public void Enter()
    {
        endTime = Time.time + boss.IntroSeconds;
        boss.Movement?.SetBehavior(BossMoveBehavior.Hold);
    }

    public void Tick()
    {
        if (Time.time >= endTime)
            boss.StateMachine.ChangeState(boss.CombatState);
    }

    public void FixedTick() { }
    public void Exit() { }
}
