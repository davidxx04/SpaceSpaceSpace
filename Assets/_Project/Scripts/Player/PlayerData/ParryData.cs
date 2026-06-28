using UnityEngine;

// Datos ajustables del parry, como asset (mismo patrón que RollData/AttackData).
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Parry Data", fileName = "ParryData")]
public class ParryData : ScriptableObject
{
    [Tooltip("Duración total del estado de parry, en segundos.")]
    public float duration = 0.3f;

    [Header("Ventana activa (cuándo bloqueas de verdad)")]
    [Range(0f, 1f)]
    [Tooltip("Inicio de la ventana de parry, como fracción de la duración.")]
    public float parryWindowStart = 0f;

    [Range(0f, 1f)]
    [Tooltip("Fin de la ventana de parry, como fracción de la duración.")]
    public float parryWindowEnd = 0.4f;

    [Header("Encadenado / movimiento")]
    [Tooltip("Ventana de cancelación: cuánto ANTES de que termine el parry se puede ya empezar otra acción.")]
    public float cancelWindow = 0f;

    [Tooltip("Al ACERTAR un parry: cancela su cooldown y permite encadenar cualquier acción al " +
             "instante (como una cancel window). El re-parry queda disponible al momento.")]
    public bool cancelOnSuccess = true;

    [Tooltip("Multiplica la velocidad de caminado durante el parry. 0 = anclado.")]
    public float moveMultiplier = 0f;

    [Header("Cooldown / Feedback")]
    [Tooltip("Tiempo desde que empieza un parry hasta que se puede volver a parrear, en segundos.")]
    public float cooldown = 0.5f;

    [Tooltip("VFX opcional a instanciar al parrear (sin uso obligatorio).")]
    public GameObject vfxPrefab;
}
