using UnityEngine;

// Tuning del movimiento del boss (un asset por "estilo" de movimiento; cada fase puede usar uno).
// Lo lee BossMovement. Datos puros, sin lógica.
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Movement Data", fileName = "BossMovementData")]
public class BossMovementData : ScriptableObject
{
    [Tooltip("Velocidad de desplazamiento del boss, en unidades/segundo.")]
    public float moveSpeed = 3f;

    [Header("Strafe (orbitar al jugador)")]
    [Tooltip("Radio de la órbita alrededor del jugador.")]
    public float strafeRadius = 5f;

    [Tooltip("Velocidad angular de la órbita, en grados/segundo (+ antihorario, - horario).")]
    public float strafeAngularSpeed = 40f;

    [Header("MaintainDistance (mantener distancia)")]
    [Tooltip("Distancia que el boss intenta mantener respecto al jugador.")]
    public float preferredDistance = 6f;

    [Header("MoveTo (reposicionarse a un punto)")]
    [Tooltip("Distancia a la que se considera 'llegado' al destino de un MoveTo.")]
    public float arrivalThreshold = 0.2f;
}
