using UnityEngine;

// Tuning del movimiento del boss (un asset por "estilo" de movimiento; cada fase puede usar uno).
// Lo lee BossMovement. Datos puros, sin lógica.
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Movement Data", fileName = "BossMovementData")]
public class BossMovementData : ScriptableObject
{
    [Tooltip("Velocidad de desplazamiento del boss, en unidades/segundo (tope, no velocidad fija).")]
    public float moveSpeed = 3f;

    [Header("Reacción / peso (naturalidad)")]
    [Tooltip("Retardo de reacción: el boss apunta a tu posición 'con lag' (SmoothDamp). Mayor = " +
             "reacciona más tarde, menos 'pegado'. 0 = instantáneo (comportamiento antiguo).")]
    public float reactionLag = 0.35f;

    [Tooltip("Inercia del desplazamiento real (acelera/frena en vez de velocidad seca). ~0 = arranque/frenada secos.")]
    public float moveSmoothTime = 0.15f;

    [Header("Strafe (orbitar al jugador)")]
    [Tooltip("Radio de la órbita alrededor del jugador.")]
    public float strafeRadius = 5f;

    [Tooltip("Velocidad angular de la órbita, en grados/segundo (+ antihorario, - horario).")]
    public float strafeAngularSpeed = 40f;

    [Header("MaintainDistance (mantener distancia)")]
    [Tooltip("Distancia que el boss intenta mantener respecto al jugador.")]
    public float preferredDistance = 6f;

    [Tooltip("Banda muerta: no corrige la distancia mientras estés dentro de ±esto de la deseada. " +
             "Mata el micro-mirroring (perseguirte sin parar).")]
    public float distanceTolerance = 1.5f;

    [Tooltip("Deriva tangencial (orbita lenta) en grados/seg → se mueve en diagonal en vez de radial puro.")]
    public float driftAngularSpeed = 22f;

    [Tooltip("Cada cuánto invierte el sentido de la deriva (rango aleatorio min/max, seg).")]
    public Vector2 driftFlipInterval = new Vector2(2.5f, 5f);

    [Tooltip("Velocidad de la 'respiración' de la distancia deseada (rad/seg): a veces se acerca, a veces se aleja.")]
    public float breatheSpeed = 0.6f;

    [Tooltip("Amplitud de la respiración: cuánto varía la distancia deseada (unidades).")]
    public float breatheAmplitude = 0.9f;

    [Header("MoveTo (reposicionarse a un punto)")]
    [Tooltip("Distancia a la que se considera 'llegado' al destino de un MoveTo.")]
    public float arrivalThreshold = 0.2f;
}
