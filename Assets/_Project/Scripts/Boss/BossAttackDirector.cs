using System.Collections;
using System.Collections.Generic;
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

    // Selección de singles/combos (secuencial / aleatorio / baraja). Estado por-lista en cada Picker.
    private readonly Picker singlePicker = new Picker();
    private readonly Picker comboPicker = new Picker();

    public void Initialize(BossContext context) => ctx = context;

    public void SetPhase(BossPhaseData newPhase)
    {
        phase = newPhase;
        singlePicker.Reset();
        comboPicker.Reset();
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
        return phase.singles[singlePicker.Next(phase.singles.Length, phase.singleSelection)];
    }

    private BossComboSO NextCombo()
    {
        if (phase.combos == null || phase.combos.Length == 0) return null;
        return phase.combos[comboPicker.Next(phase.combos.Length, phase.comboSelection)];
    }

    // Elige índices según el modo, manteniendo estado por-lista:
    //  - Sequential: 0,1,...,n-1,0,... (memorizable).
    //  - Random: azar puro (puede repetir el inmediato anterior).
    //  - Shuffle: baraja (Fisher-Yates) y va sacando; al agotarse rebaraja evitando repetir el último
    //    sacado -> aleatorio "orgánico" (sin repeticiones inmediatas ni rachas largas).
    private class Picker
    {
        private readonly List<int> bag = new List<int>();
        private int seqIndex;
        private int last = -1;

        public void Reset() { bag.Clear(); seqIndex = 0; last = -1; }

        public int Next(int count, BossPhaseData.Selection mode)
        {
            if (count <= 1) return 0;

            switch (mode)
            {
                case BossPhaseData.Selection.Sequential:
                    int s = seqIndex % count;
                    seqIndex++;
                    return s;

                case BossPhaseData.Selection.Random:
                    return Random.Range(0, count);

                default: // Shuffle
                    if (bag.Count == 0) Refill(count);
                    int idx = bag[bag.Count - 1];
                    bag.RemoveAt(bag.Count - 1);
                    last = idx;
                    return idx;
            }
        }

        private void Refill(int count)
        {
            for (int i = 0; i < count; i++) bag.Add(i);
            for (int i = bag.Count - 1; i > 0; i--)   // Fisher-Yates
            {
                int j = Random.Range(0, i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
            // El siguiente en salir es el último del bag; si repite el anterior, lo intercambia con otro.
            if (bag[bag.Count - 1] == last)
                (bag[bag.Count - 1], bag[0]) = (bag[0], bag[bag.Count - 1]);
        }
    }
}
