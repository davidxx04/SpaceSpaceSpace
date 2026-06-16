using UnityEngine;

// Datos ajustables del ataque básico, como asset (mismo patrón que RollData).
// El ataque cargado del futuro será simplemente OTRO asset AttackData con números
// mayores, sin tocar código.
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Attack Data", fileName = "AttackData")]
public class AttackData : ScriptableObject
{
    [Header("Daño")]
    public float damage = 10f;

    [Header("Tiempos")]
    [Tooltip("Duración total del estado de ataque, en segundos.")]
    public float duration = 0.3f;

    [Range(0f, 1f)]
    [Tooltip("Fracción de la duración en la que la hitbox se ACTIVA. ~0 = casi instantáneo.")]
    public float hitStart = 0.05f;

    [Range(0f, 1f)]
    [Tooltip("Fracción de la duración en la que la hitbox se DESACTIVA.")]
    public float hitEnd = 0.35f;

    [Header("Hitbox")]
    [Tooltip("Distancia a la que se coloca la hitbox por delante, en la dirección de apuntado.")]
    public float hitboxDistance = 1f;

    [Tooltip("Tamaño de la caja de golpeo (ancho x alto).")]
    public Vector2 hitboxSize = new Vector2(1f, 1f);

    [Header("Movimiento durante el ataque")]
    [Tooltip("Multiplica la velocidad de caminado mientras se ataca. " +
             "0 = anclado, 1 = igual, >1 = más rápido.")]
    public float moveMultiplier = 0f;

    [Header("Cooldown / Feedback")]
    [Tooltip("Tiempo desde que empieza un ataque hasta que se puede volver a atacar, en segundos.")]
    public float cooldown = 0.4f;

    [Tooltip("VFX opcional a instanciar al atacar (sin uso obligatorio).")]
    public GameObject vfxPrefab;

    [Header("Debug")]
    [Tooltip("Dibuja la caja de la hitbox con gizmos: gris en reposo, verde mientras golpea.")]
    public bool showDebugHitbox;
}
