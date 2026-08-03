using UnityEngine;

// Una fase de dificultad del boss: qué ataques/combos tiene disponibles y con qué ritmo (agresividad).
// El BossController guarda un BossPhaseData[] ORDENADO de más vida a menos (enterAtHealthFraction
// descendente: 1.0, 0.66, 0.33...) y activa la fase más avanzada cuyo umbral ya se haya cruzado.
//
// Cambiar la escalada = editar estos assets. Cambiar el DISPARADOR (vida -> tiempo, postura, etc.)
// = una línea en BossController.UpdatePhase().
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Phase Data", fileName = "BossPhaseData")]
public class BossPhaseData : ScriptableObject
{
    [Tooltip("Identificador para depurar (phase1_warmup, phase2_combos...).")]
    public string id = "phase";

    [Range(0f, 1f)]
    [Tooltip("Esta fase se activa cuando la vida del boss (Current/Max) baja a este valor o menos.")]
    public float enterAtHealthFraction = 1f;

    [Header("Repertorio")]
    [Tooltip("Si está activo y hay combos, el boss lanza COMBOS; si no, ataques SUELTOS.")]
    public bool useCombos = false;

    [Tooltip("Ataques sueltos disponibles en esta fase.")]
    public BossAttackSO[] singles;

    [Tooltip("Combos disponibles en esta fase (se usan si useCombos = true).")]
    public BossComboSO[] combos;

    public enum Selection { Sequential, Random, Shuffle }

    [Tooltip("Cómo se eligen los ATAQUES SUELTOS. Shuffle = aleatorio orgánico (baraja sin repetir el " +
             "inmediato anterior) = se siente natural. Random = azar puro. Sequential = en orden.")]
    public Selection singleSelection = Selection.Shuffle;

    [Tooltip("Cómo se eligen los COMBOS. Sequential = en orden (memorizable, recomendado). " +
             "Shuffle/Random = variar el orden.")]
    public Selection comboSelection = Selection.Sequential;

    [Range(0f, 1f)]
    [Tooltip("Si useCombos y hay singles: probabilidad de lanzar un COMBO (vs un single suelto) en cada " +
             "acción. 1 = solo combos; 0.5 = mitad combos / mitad singles intercalados.")]
    public float comboChance = 1f;

    [Header("Agresividad")]
    [Tooltip("Pausa entre acciones, en segundos (rango aleatorio min/max). Menor = más agresivo.")]
    public Vector2 gapRange = new Vector2(2f, 3f);

    [Header("Movimiento")]
    [Tooltip("Comportamiento de movimiento ambiente durante esta fase.")]
    public BossMoveBehavior moveBehavior = BossMoveBehavior.Hold;

    [Tooltip("Tuning de movimiento para esta fase (opcional).")]
    public BossMovementData movement;

    [Header("Ritmo de combo")]
    [Range(0.25f, 1f)]
    [Tooltip("Escala del tiempo ENTRE primitivas de los combos (delayAfter y launchInterval). " +
             "1 = normal; 0.75 = los combos van al 75% del tiempo (más rápidos). Solo afecta a los " +
             "huecos internos del combo, no a la duración de cada primitiva.")]
    public float comboDelayScale = 1f;
}
