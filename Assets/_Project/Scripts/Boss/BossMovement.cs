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

    // --- Naturalidad (de-glue): el boss persigue un ANCLA retardada del jugador, no su posición real ---
    private Vector2 playerAnchor;            // posición del jugador 'con lag' (la que de verdad persigue)
    private Vector2 anchorVel;               // velocidad interna del SmoothDamp del ancla
    private Vector2 moveVel;                 // velocidad interna del SmoothDamp del desplazamiento (peso/inercia)
    private float driftSign = 1f;            // sentido actual de la deriva tangencial
    private float nextDriftFlipTime;         // próximo instante en que invierte la deriva

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
    public void SetPlayer(Transform p)
    {
        player = p;
        // El ancla arranca sobre el jugador para no pegar un tirón en el primer frame.
        playerAnchor = p != null ? (Vector2)p.position : rb.position;
        anchorVel = Vector2.zero;
    }
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

        float dt = Time.fixedDeltaTime;
        Vector2 pos = rb.position;
        Vector2 target = pos;

        // ANCLA RETARDADA: el boss persigue una versión 'con lag' de tu posición, no la real. Esto
        // es lo que rompe el espejo 1:1 (la sensación de que mueves al boss). reactionLag=0 => al día.
        if (player != null)
            playerAnchor = Vector2.SmoothDamp(playerAnchor, player.position, ref anchorVel,
                                              data.reactionLag, Mathf.Infinity, dt);

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
                        strafeAngle += data.strafeAngularSpeed * dt;
                        float rad = strafeAngle * Mathf.Deg2Rad;
                        target = playerAnchor + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * data.strafeRadius;
                    }
                    break;

                case BossMoveBehavior.MaintainDistance:
                    if (player != null) target = MaintainDistanceTarget(pos, dt);
                    break;
            }
        }

        target = ClampToArena(target);

        // INTEGRADOR CON PESO: SmoothDamp (críticamente amortiguado, sin overshoot) en vez de
        // velocidad constante → arranques y frenadas suaves. moveSpeed actúa como tope de velocidad.
        Vector2 next = Vector2.SmoothDamp(pos, target, ref moveVel, data.moveSmoothTime, data.moveSpeed, dt);
        rb.MovePosition(ClampToArena(next));
    }

    // MaintainDistance en coordenadas polares alrededor del ANCLA retardada: banda muerta (no corrige
    // dentro de ±tolerancia), respiración (la distancia deseada oscila) y deriva tangencial (orbita
    // lenta que invierte el sentido cada pocos segundos) → diagonal viva en vez de radial-espejo.
    private Vector2 MaintainDistanceTarget(Vector2 pos, float dt)
    {
        Vector2 fromAnchor = pos - playerAnchor;
        float dist = fromAnchor.magnitude;
        if (dist < 0.0001f) { fromAnchor = Vector2.up; dist = data.preferredDistance; }
        float angle = Mathf.Atan2(fromAnchor.y, fromAnchor.x);

        // Flip ocasional del sentido de la deriva (menos mecánico que orbitar siempre igual).
        if (Time.time >= nextDriftFlipTime)
        {
            driftSign = Random.value < 0.5f ? 1f : -1f;
            nextDriftFlipTime = Time.time + Random.Range(data.driftFlipInterval.x, data.driftFlipInterval.y);
        }
        angle += data.driftAngularSpeed * driftSign * Mathf.Deg2Rad * dt;

        // Respiración: la distancia deseada oscila → a veces se acerca, a veces se aleja.
        float desired = data.preferredDistance + Mathf.Sin(Time.time * data.breatheSpeed) * data.breatheAmplitude;
        // Banda muerta: dentro de [desired±tol] el Clamp devuelve dist (no corrige); fuera, trae al borde.
        float radius = Mathf.Clamp(dist, desired - data.distanceTolerance, desired + data.distanceTolerance);

        return playerAnchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
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
