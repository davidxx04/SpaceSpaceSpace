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
    public Projectile ProjectilePrefab;  // bala NO parreable (su Hitbox debe apuntar a la capa Player)
    public Projectile ParryableProjectilePrefab;  // bala PARREABLE (otro prefab/sprite); opcional
    public BossArea AreaPrefab;          // prefab del área (zona telegrafiada por barra de relleno); opcional
    public BossSwoosh SwooshPrefab;      // prefab del swoosh (barra que barre); opcional
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
        // El prefab VISUAL lo decide 'parryable' (código de color: bala normal roja vs bala parreable
        // cian, cada una su propio prefab/sprite/animación). Si no hay prefab parreable, usa el normal.
        Projectile prefab = parryable && ParryableProjectilePrefab != null
            ? ParryableProjectilePrefab
            : ProjectilePrefab;

        if (prefab == null)
        {
            Debug.LogWarning("[BossContext] No hay prefab de bala asignado; no se dispara.");
            return null;
        }

        // Nace ya orientada hacia 'dir' (no identidad) para evitar el frame de rotación torcida.
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Projectile proj = PoolManager.Spawn(prefab, pos, Quaternion.Euler(0f, 0f, angle));
        if (proj == null) return null;
        var info = new DamageInfo(damage, Boss != null ? Boss.gameObject : null, dir) { parryable = parryable };
        proj.Launch(dir, speed, range, pierce, info);
        return proj;
    }

    // Instancia un área de ataque pooleada en 'pos'. La configura y dirige el AreaAttackSO.
    public BossArea SpawnArea(Vector2 pos)
    {
        if (AreaPrefab == null)
        {
            Debug.LogWarning("[BossContext] No hay prefab de área asignado; no se lanza el área.");
            return null;
        }
        return PoolManager.Spawn(AreaPrefab, pos, Quaternion.identity);
    }

    // Instancia un swoosh pooleado en 'pos'. Lo configura y dirige el SwooshAttackSO.
    public BossSwoosh SpawnSwoosh(Vector2 pos)
    {
        if (SwooshPrefab == null)
        {
            Debug.LogWarning("[BossContext] No hay prefab de swoosh asignado; no se lanza el swoosh.");
            return null;
        }
        return PoolManager.Spawn(SwooshPrefab, pos, Quaternion.identity);
    }
}
