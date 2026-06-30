using UnityEngine;

// Datos ajustables del ataque a distancia, como asset (mismo patrón que RollData/ParryData).
// Un "ataque cargado" futuro será simplemente OTRO asset AttackData con números mayores.
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Attack Data", fileName = "AttackData")]
public class AttackData : ScriptableObject
{
    [Header("Daño")]
    public float damage = 10f;

    [Header("Tiempos")]
    [Tooltip("Duración total del estado de ataque (la recuperación tras disparar), en segundos.")]
    public float duration = 0.25f;

    [Range(0f, 1f)]
    [Tooltip("Fracción de la duración en la que SALE el disparo. ~0 = casi instantáneo.")]
    public float fireTime = 0.05f;

    [Tooltip("Tiempo desde que disparas hasta que puedes volver a disparar (cadencia), en segundos.")]
    public float cooldown = 0.3f;

    [Tooltip("Si está marcado, disparas en AUTOMÁTICO mientras MANTIENES pulsado (cadencia = cooldown).")]
    public bool automatic = false;

    [Tooltip("Ventana de cancelación: cuánto ANTES de que termine el ataque puedes ya empezar otra acción.")]
    public float cancelWindow = 0f;

    [Tooltip("Multiplica la velocidad de caminado mientras disparas. 0 = anclado, 1 = igual.")]
    public float moveMultiplier = 1f;

    [Tooltip("Si está marcado, recibir daño CANCELA el disparo en curso (evita pasarte el juego " +
             "disparando estático mientras encajas golpes). Desmárcalo para comparar si se siente mal.")]
    public bool interruptOnHit = true;

    [Header("Proyectil")]
    public Projectile projectilePrefab;

    [Tooltip("A qué distancia del centro nace la bala (el morro de la nave).")]
    public float spawnOffset = 0.6f;

    public float projectileSpeed = 12f;

    [Tooltip("Alcance máximo en unidades; la bala muere al recorrerlo o al impactar.")]
    public float range = 8f;

    [Tooltip("Si la bala atraviesa enemigos (true) o muere al primer impacto (false).")]
    public bool pierce = false;

    [Header("Patrón (bullet-hell)")]
    [Min(1)]
    [Tooltip("Número de balas por disparo.")]
    public int projectileCount = 1;

    [Tooltip("Apertura total del abanico en grados (solo si projectileCount > 1).")]
    public float spreadAngle = 0f;

    [Header("Recoil (retroceso al disparar)")]
    [Tooltip("Cuánto retrocede la nave al disparar (rebote hacia atrás).")]
    public float recoilDistance = 0.2f;

    [Tooltip("Tiempo en recuperar la posición tras el retroceso, en segundos.")]
    public float recoilRecovery = 0.15f;

    [Tooltip("false = solo rebota el sprite (visual). true = empuja al jugador de verdad (físico).")]
    public bool recoilMovesPlayer = false;

    [Header("Feedback")]
    [Tooltip("VFX opcional (fogonazo) al disparar.")]
    public GameObject vfxPrefab;
}
