using UnityEngine;

// Puente entre los ataques (ScriptableObjects, que no pueden guardar referencias de escena ni estado
// mutable por instancia) y la escena. Se crea UNA vez en BossController.Awake y se pasa a cada
// BossAttackSO.Execute. Concentra todo lo que un ataque necesita: a quién apuntar, desde dónde
// disparar y cómo instanciar balas (envolviendo el patrón de Projectile.Launch + DamageInfo).
public class BossContext
{
    public Transform Boss;               // raíz del boss (también el 'source' del daño / IParryable)
    public Transform Player;             // objetivo
    public MonoBehaviour Runner;         // el Director: para StartCoroutine si un ataque lanza corrutinas en paralelo
    public Transform[] Muzzles;          // puntos de disparo opcionales (cañones)
    public Projectile ProjectilePrefab;  // bala del boss (su Hitbox debe apuntar a la capa Player)
    public BossMovement Movement;        // movimiento, por si un ataque quiere reposicionar

    // Dirección normalizada desde 'from' hacia el jugador (Vector2.up de fallback si no hay jugador).
    public Vector2 AimToPlayer(Vector2 from)
    {
        if (Player == null) return Vector2.up;
        Vector2 d = (Vector2)Player.position - from;
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.up;
    }

    // Posición de un cañón por índice; si no hay cañones configurados, usa la posición del boss.
    public Vector2 MuzzlePosition(int index)
    {
        if (Muzzles != null && Muzzles.Length > 0)
        {
            int i = Mathf.Clamp(index, 0, Muzzles.Length - 1);
            if (Muzzles[i] != null) return Muzzles[i].position;
        }
        return Boss != null ? (Vector2)Boss.position : Vector2.zero;
    }

    // Instancia y dispara una bala. Reproduce exactamente el patrón de AttackState.Fire:
    // instanciar el prefab -> construir DamageInfo (con 'source' = boss para que el parry pueda
    // notificar a IParryable) -> Launch. 'parryable' decide bullet-hell (false) vs sekiro (true).
    public Projectile Spawn(Vector2 pos, Vector2 dir, float speed, float range, bool pierce, float damage, bool parryable)
    {
        if (ProjectilePrefab == null)
        {
            Debug.LogWarning("[BossContext] No hay ProjectilePrefab asignado; no se dispara.");
            return null;
        }

        Projectile proj = PoolManager.Spawn(ProjectilePrefab, pos, Quaternion.identity);
        if (proj == null) return null;
        var info = new DamageInfo(damage, Boss != null ? Boss.gameObject : null, dir) { parryable = parryable };
        proj.Launch(dir, speed, range, pierce, info);
        return proj;
    }
}
