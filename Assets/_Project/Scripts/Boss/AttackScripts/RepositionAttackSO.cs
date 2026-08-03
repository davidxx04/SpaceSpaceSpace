using System.Collections;
using UnityEngine;

// "Ataque" SIN daño = movimiento-como-paso (§10.4). Mueve al boss a un ancla con BossMovement.MoveTo y
// espera (yield) hasta HasArrived (con timeout de seguridad). Sirve para meterlo en un combo y CAMBIAR
// LA GEOMETRÍA del siguiente patrón (p. ej. ir a un borde para que el área barra otra mitad de arena).
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Boss/Attacks/Reposition", fileName = "RepositionAttack")]
public class RepositionAttackSO : BossAttackSO
{
    public enum Mode { Center, Edge, AwayFromPlayer }

    [Header("Reposicionamiento (sin daño)")]
    public Mode mode = Mode.Edge;

    [Tooltip("Margen hacia dentro del borde de la arena, en unidades.")]
    public float margin = 2f;

    [Tooltip("Tiempo máximo de espera por si no llega (seguridad), en segundos.")]
    public float timeout = 2.5f;

    [Header("Velocidad (opcional)")]
    [Tooltip("Si > 0, ANULA la velocidad ambiente de la fase SOLO para este desplazamiento (p.ej. un " +
             "cruce rápido del arena). 0 = hereda BossMovementData.moveSpeed de la fase actual, como hoy.")]
    public float travelSpeed = 0f;

    public override IEnumerator Execute(BossContext ctx)
    {
        if (ctx.Movement == null) yield break;

        // NOTA: telegraphSeconds/recoverySeconds (heredados de BossAttackSO) antes eran letra muerta
        // aquí -> ahora SÍ se respetan. Si algún asset existente los tenía puestos "por si acaso"
        // (p.ej. util_RepositionEdge), su timing real cambia; ajústalos a 0 si no se quiere el aviso.
        yield return Telegraph(ctx);

        ctx.Movement.MoveTo(ComputeTarget(ctx), travelSpeed);

        float t = 0f;
        while (!ctx.Movement.HasArrived && t < timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }

        yield return Recover();
    }

    private Vector2 ComputeTarget(BossContext ctx)
    {
        Rect a = ArenaBounds.PlayArea;
        Vector2 boss = ctx.Boss != null ? (Vector2)ctx.Boss.position : Vector2.zero;
        if (a.width <= 0f || a.height <= 0f) return boss;   // sin arena: no se mueve

        switch (mode)
        {
            case Mode.Center:
                return a.center;

            case Mode.AwayFromPlayer:
            {
                Vector2 player = ctx.Player != null ? (Vector2)ctx.Player.position : a.center;
                Vector2 away = boss - player;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;
                Vector2 p = a.center + away * (Mathf.Min(a.width, a.height) * 0.5f - margin);
                return Clamp(p, a, margin);
            }

            case Mode.Edge:
            default:
            {
                // Un borde aleatorio: cambia el "escenario" del siguiente patrón.
                bool pinX = Random.value < 0.5f;
                float x = pinX ? (Random.value < 0.5f ? a.xMin + margin : a.xMax - margin)
                               : Mathf.Lerp(a.xMin + margin, a.xMax - margin, Random.value);
                float y = pinX ? Mathf.Lerp(a.yMin + margin, a.yMax - margin, Random.value)
                               : (Random.value < 0.5f ? a.yMin + margin : a.yMax - margin);
                return new Vector2(x, y);
            }
        }
    }

    private static Vector2 Clamp(Vector2 p, Rect a, float m)
        => new Vector2(Mathf.Clamp(p.x, a.xMin + m, a.xMax - m), Mathf.Clamp(p.y, a.yMin + m, a.yMax - m));
}
