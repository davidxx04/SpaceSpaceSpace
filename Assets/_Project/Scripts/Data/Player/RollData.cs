using UnityEngine;

// Datos ajustables del rol/esquiva del jugador.
// Vive como un asset en la ventana Project, de modo que el rol se balancea desde el
// Inspector (incluida la curva de movimiento). Los cambios sobre el asset PERSISTEN al
// salir del Play mode, lo que permite afinar el game feel "en caliente".
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Roll Data", fileName = "RollData")]
public class RollData : ScriptableObject
{
    [Tooltip("Distancia total que recorre el rol, en unidades del mundo.")]
    public float distance = 3f;

    [Tooltip("Tiempo total que dura el rol, en segundos.")]
    public float duration = 0.3f;

    [Tooltip("Reparte la distancia a lo largo de la duración. Eje X = tiempo (0..1), " +
             "eje Y = progreso recorrido (0..1). Por defecto: rápido al inicio y lento al final.")]
    public AnimationCurve movementCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2f),   // arranca con pendiente alta -> rápido
        new Keyframe(1f, 1f, 0f, 0f));  // termina plano -> lento

    [Header("Inmunidad (i-frames)")]
    [Range(0f, 1f)]
    [Tooltip("Inicio de la ventana de invulnerabilidad, como fracción de la duración.")]
    public float iFrameStart = 0f;

    [Range(0f, 1f)]
    [Tooltip("Fin de la ventana de invulnerabilidad, como fracción de la duración.")]
    public float iFrameEnd = 0.5f;

    [Header("Cooldown")]
    [Tooltip("Tiempo desde que empieza un rol hasta que se puede volver a rodar, en segundos.")]
    public float cooldown = 0.8f;

    [Tooltip("Ventana de cancelación: cuánto ANTES de que termine el rol se puede ya empezar " +
             "otra acción (ataque, parry...). 0 = acción totalmente comprometida.")]
    public float cancelWindow = 0f;

    [Header("Feedback (opcional, sin uso aún)")]
    [Tooltip("VFX a instanciar al iniciar el rol. Aún no se consume gameplay.")]
    public GameObject vfxPrefab;

    [Tooltip("Estela (afterimage) que se emite mientras dura el rol. Vacío = sin estela.")]
    public AfterimageData afterimage;
}
