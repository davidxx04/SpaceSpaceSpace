using System.Collections;
using UnityEngine;

// El "cerebro" que decide QUÉ lanza el boss y con qué ritmo. Es un MonoBehaviour porque corre las
// corrutinas de los ataques. No sabe nada de cómo es cada ataque: solo los ejecuta. La agresividad
// y el repertorio vienen de la fase actual (BossPhaseData), que le fija el BossController.
//
// Ciclo: esperar un hueco (gap aleatorio de la fase) -> elegir suelto o combo -> ejecutarlo -> repetir.
public class BossAttackDirector : MonoBehaviour
{
    private BossContext ctx;
    private BossPhaseData phase;
    private Coroutine loop;

    // Índices para el modo Sequential (repertorio en orden = memorizable).
    private int singleIndex;
    private int comboIndex;

    public void Initialize(BossContext context) => ctx = context;

    public void SetPhase(BossPhaseData newPhase)
    {
        phase = newPhase;
        singleIndex = 0;
        comboIndex = 0;
    }

    // Arranca el bucle de ataques. Lo llama BossCombatState.Enter().
    public void Begin()
    {
        Stop();
        loop = StartCoroutine(Loop());
    }

    // Para el bucle y corta cualquier ataque en curso. Lo llama Combat.Exit() (al entrar en Stagger/Defeated).
    // Las balas ya disparadas siguen su curso (correcto); solo dejamos de generar nuevas.
    public void Stop()
    {
        if (loop != null) { StopCoroutine(loop); loop = null; }
        StopAllCoroutines();
    }

    private IEnumerator Loop()
    {
        while (true)
        {
            if (phase == null) { yield return null; continue; }

            float gap = Random.Range(phase.gapRange.x, phase.gapRange.y);
            if (gap > 0f) yield return new WaitForSeconds(gap);

            bool doCombo = phase.useCombos && phase.combos != null && phase.combos.Length > 0;

            if (doCombo) yield return RunCombo(NextCombo());
            else yield return RunAttack(NextSingle());
        }
    }

    private IEnumerator RunAttack(BossAttackSO attack)
    {
        if (attack == null) { yield return null; yield break; }
        yield return attack.Execute(ctx);
    }

    private IEnumerator RunCombo(BossComboSO combo)
    {
        if (combo == null || combo.sequence == null) { yield return null; yield break; }

        foreach (BossComboSO.Step step in combo.sequence)
        {
            yield return RunAttack(step.attack);
            if (step.delayAfter > 0f) yield return new WaitForSeconds(step.delayAfter);
        }
    }

    private BossAttackSO NextSingle()
    {
        if (phase.singles == null || phase.singles.Length == 0) return null;
        if (phase.selection == BossPhaseData.Selection.Random)
            return phase.singles[Random.Range(0, phase.singles.Length)];

        BossAttackSO a = phase.singles[singleIndex % phase.singles.Length];
        singleIndex++;
        return a;
    }

    private BossComboSO NextCombo()
    {
        if (phase.combos == null || phase.combos.Length == 0) return null;
        if (phase.selection == BossPhaseData.Selection.Random)
            return phase.combos[Random.Range(0, phase.combos.Length)];

        BossComboSO c = phase.combos[comboIndex % phase.combos.Length];
        comboIndex++;
        return c;
    }
}
