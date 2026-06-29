using UnityEngine;

// Comportamiento de movimiento "ambiente" del boss (qué hace mientras no se le ordena nada).
public enum BossMoveBehavior
{
    Hold,             // quieto en su sitio
    Strafe,           // orbita alrededor del jugador a un radio fijo
    MaintainDistance, // se acerca/aleja para mantener una distancia del jugador
}

// Movimiento del boss, DESACOPLADO de los ataques. Vive en FixedUpdate y mueve un Rigidbody2D
// Kinematic con MovePosition (predecible, sin knockback de balas). El Director/fase fija el
// comportamiento ambiente; un ataque puede pedir un reposicionamiento puntual con MoveTo(point)
// y esperar (yield) hasta HasArrived. La posición se clampa dentro de ArenaBounds.PlayArea.
//
// Convención del proyecto: Unity 2022.3 -> se usa rb.velocity / MovePosition, NO linearVelocity.
[RequireComponent(typeof(Rigidbody2D))]
public class BossMovement : MonoBehaviour
{
    [SerializeField] private BossMovementData data;

    [Header("Límites")]
    [Tooltip("SpriteRenderer del cuerpo: su medio-tamaño se resta del borde para que el sprite ENTERO " +
             "quepa siempre en pantalla (no medio fuera). Vacío = autodetecta el primero en los hijos.")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    [Tooltip("Margen extra hacia dentro, además del medio-sprite (ajuste fino).")]
    [SerializeField] private Vector2 edgeMargin = Vector2.zero;

    private Rigidbody2D rb;
    private Vector2 halfSize;   // medio-tamaño del sprite del boss (para insetar el clamp de la arena)
    private Transform player;
    private BossMoveBehavior behavior = BossMoveBehavior.Hold;
    private Vector2? moveToTarget;   // si tiene valor, manda sobre 'behavior' hasta llegar
    private float strafeAngle;       // ángulo actual de la órbita (grados)

    // --- Embestida (movimiento-como-ataque) ---
    private bool charging;
    private Vector2 chargeDir;
    private float chargeSpeed, chargeApproach, chargeOvershoot, chargeTraveled;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyRenderer != null) halfSize = bodyRenderer.bounds.extents;   // medio-tamaño en mundo (cuenta la escala)
    }

    // --- API que usan el controller, los estados y los ataques ---
    public void SetPlayer(Transform p) => player = p;
    public void SetData(BossMovementData d) { if (d != null) data = d; }
    public void SetBehavior(BossMoveBehavior b) { behavior = b; moveToTarget = null; }

    // Pide un reposicionamiento puntual a un punto del mundo (sobrescribe el comportamiento ambiente).
    public void MoveTo(Vector2 worldPoint) => moveToTarget = worldPoint;

    // true cuando no hay un MoveTo pendiente (el ataque puede 'yield' hasta que sea true).
    public bool HasArrived => !moveToTarget.HasValue;

    // Embestida: se lanza a 'speed' en 'dir' a velocidad plena durante 'approach' (la distancia hasta
    // el jugador) y luego FRENA con ease-out a lo largo de 'overshoot' tras sobrepasarlo. Mientras
    // IsCharging, manda sobre el comportamiento ambiente. Lo usa ChargeAttackSO (movimiento-como-ataque).
    public bool IsCharging => charging;
    public void Charge(Vector2 dir, float approach, float overshoot, float speed)
    {
        chargeDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
        chargeApproach = Mathf.Max(0f, approach);
        chargeOvershoot = Mathf.Max(0.1f, overshoot);
        chargeSpeed = Mathf.Max(0.1f, speed);
        chargeTraveled = 0f;
        charging = true;
        moveToTarget = null;
    }

    // Avance de la embestida: velocidad plena hasta sobrepasar al jugador, luego ease-out (SmoothStep)
    // hasta una fracción mínima para terminar limpio. Respeta los límites de la arena.
    private void TickCharge()
    {
        float total = chargeApproach + chargeOvershoot;
        float speed = chargeSpeed;
        if (chargeTraveled > chargeApproach)
        {
            float t = Mathf.Clamp01((chargeTraveled - chargeApproach) / chargeOvershoot);
            speed = Mathf.Lerp(chargeSpeed, chargeSpeed * 0.08f, Mathf.SmoothStep(0f, 1f, t));
        }

        float step = Mathf.Min(speed * Time.fixedDeltaTime, total - chargeTraveled);
        Vector2 next = ClampToArena(rb.position + chargeDir * step);
        bool blocked = (next - rb.position).sqrMagnitude < 1e-8f && step > 1e-5f;  // pared
        rb.MovePosition(next);
        chargeTraveled += step;

        if (chargeTraveled >= total - 1e-3f || blocked) charging = false;
    }

    private void FixedUpdate()
    {
        if (charging) { TickCharge(); return; }
        if (data == null) return;

        Vector2 pos = rb.position;
        Vector2 target = pos;

        if (moveToTarget.HasValue)
        {
            target = moveToTarget.Value;
            if (Vector2.Distance(pos, target) <= data.arrivalThreshold) moveToTarget = null;
        }
        else
        {
            switch (behavior)
            {
                case BossMoveBehavior.Hold:
                    target = pos;
                    break;

                case BossMoveBehavior.Strafe:
                    if (player != null)
                    {
                        strafeAngle += data.strafeAngularSpeed * Time.fixedDeltaTime;
                        float rad = strafeAngle * Mathf.Deg2Rad;
                        target = (Vector2)player.position + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * data.strafeRadius;
                    }
                    break;

                case BossMoveBehavior.MaintainDistance:
                    if (player != null)
                    {
                        Vector2 fromPlayer = pos - (Vector2)player.position;
                        if (fromPlayer.sqrMagnitude < 0.0001f) fromPlayer = Vector2.up;
                        target = (Vector2)player.position + fromPlayer.normalized * data.preferredDistance;
                    }
                    break;
            }
        }

        target = ClampToArena(target);

        Vector2 next = Vector2.MoveTowards(pos, target, data.moveSpeed * Time.fixedDeltaTime);
        rb.MovePosition(next);
    }

    // Clampa el CENTRO del boss dejando un margen = medio-sprite (+ extra) hacia dentro, para que el
    // sprite entero quede siempre dentro de la pantalla en vez de medio fuera.
    private Vector2 ClampToArena(Vector2 p)
    {
        Rect a = ArenaBounds.PlayArea;
        if (a.width <= 0f || a.height <= 0f) return p;   // arena aún no construida

        float mx = halfSize.x + edgeMargin.x;
        float my = halfSize.y + edgeMargin.y;
        float xMin = a.xMin + mx, xMax = a.xMax - mx;
        float yMin = a.yMin + my, yMax = a.yMax - my;
        if (xMin > xMax) xMin = xMax = a.center.x;   // sprite más ancho que la arena: queda centrado
        if (yMin > yMax) yMin = yMax = a.center.y;

        return new Vector2(Mathf.Clamp(p.x, xMin, xMax), Mathf.Clamp(p.y, yMin, yMax));
    }
}
