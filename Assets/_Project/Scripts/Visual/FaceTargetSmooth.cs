using UnityEngine;

// Rota este transform para "mirar" a un objetivo (el jugador por defecto) con un pequeño retraso
// suavizado (SmoothDampAngle) -> se siente natural, no un snap. Genérico y reutilizable; hoy lo usa
// el boss para seguir al jugador con la mirada.
//
// Como la NAVE DEL JUGADOR (PlayerController.UpdateAim): el sprite mira a la DERECHA (+X); se rota al
// ángulo de apuntado y, para NO quedar boca abajo al apuntar a la izquierda, se voltea en vertical
// (flipY) en vez de girar del revés -> el "arriba" del sprite siempre queda arriba. No toca posición
// ni física; el boss se mueve por su cuenta (BossMovement, Kinematic) y su collider es un círculo,
// así que rotar no afecta al gameplay.
public class FaceTargetSmooth : MonoBehaviour
{
    [Tooltip("A quién mirar. Vacío = autolocaliza el PlayerController de la escena.")]
    [SerializeField] private Transform target;

    [Tooltip("Sprite a voltear (flipY) para no quedar boca abajo. Vacío = SpriteRenderer de este objeto.")]
    [SerializeField] private SpriteRenderer flipSprite;

    [Tooltip("Retraso del giro (segundos aprox. del SmoothDamp). Mayor = más perezoso/natural.")]
    [SerializeField, Min(0f)] private float turnSmoothTime = 0.15f;

    [Tooltip("Ángulo (grados) hacia el que 'mira' el sprite en su orientación base: 0 = derecha (+X). " +
             "Este boss mira a la derecha, igual que la nave.")]
    [SerializeField] private float spriteForwardDegrees = 0f;

    [Tooltip("Tope de velocidad de giro (grados/segundo) del suavizado.")]
    [SerializeField, Min(1f)] private float maxTurnSpeed = 720f;

    // Banda muerta cerca de la vertical: dentro de ella el lado se mantiene, para no parpadear el
    // flipY cuando el morro apunta casi recto arriba/abajo (equivale al |AimDirection.x|>0.1 de la nave).
    private const float SideDeadZone = 0.1f;

    private float turnVel;       // velocidad interna del SmoothDampAngle
    private bool facingLeft;     // lado actual (mirando a la izquierda) -> flipY

    private void Awake()
    {
        if (flipSprite == null) flipSprite = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (target == null)
        {
            var pc = FindObjectOfType<PlayerController>();
            if (pc != null) target = pc.transform;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector2 dir = (Vector2)target.position - (Vector2)transform.position;
        if (dir.sqrMagnitude < 1e-6f) return;   // encima: mantiene ángulo y lado

        float desired = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - spriteForwardDegrees;
        float current = transform.eulerAngles.z;
        float z = Mathf.SmoothDampAngle(current, desired, ref turnVel, turnSmoothTime, maxTurnSpeed);
        transform.rotation = Quaternion.Euler(0f, 0f, z);

        // Volteo anti-boca-abajo según el ángulo YA MOSTRADO (no el target): el flip coincide con el
        // cruce VISUAL de la vertical al haber suavizado. Sprite mira a +X -> apunta a la izquierda
        // cuando cos(z) < 0; flipY lo endereza.
        float cx = Mathf.Cos(z * Mathf.Deg2Rad);
        if (cx < -SideDeadZone) facingLeft = true;
        else if (cx > SideDeadZone) facingLeft = false;   // dentro de la banda: se mantiene el lado

        if (flipSprite != null) flipSprite.flipY = facingLeft;
    }
}
