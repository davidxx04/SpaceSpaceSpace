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

    private Rigidbody2D rb;
    private Transform player;
    private BossMoveBehavior behavior = BossMoveBehavior.Hold;
    private Vector2? moveToTarget;   // si tiene valor, manda sobre 'behavior' hasta llegar
    private float strafeAngle;       // ángulo actual de la órbita (grados)

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    // --- API que usan el controller, los estados y los ataques ---
    public void SetPlayer(Transform p) => player = p;
    public void SetData(BossMovementData d) { if (d != null) data = d; }
    public void SetBehavior(BossMoveBehavior b) { behavior = b; moveToTarget = null; }

    // Pide un reposicionamiento puntual a un punto del mundo (sobrescribe el comportamiento ambiente).
    public void MoveTo(Vector2 worldPoint) => moveToTarget = worldPoint;

    // true cuando no hay un MoveTo pendiente (el ataque puede 'yield' hasta que sea true).
    public bool HasArrived => !moveToTarget.HasValue;

    private void FixedUpdate()
    {
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

    private static Vector2 ClampToArena(Vector2 p)
    {
        Rect a = ArenaBounds.PlayArea;
        if (a.width <= 0f || a.height <= 0f) return p;   // arena aún no construida
        return new Vector2(Mathf.Clamp(p.x, a.xMin, a.xMax), Mathf.Clamp(p.y, a.yMin, a.yMax));
    }
}
