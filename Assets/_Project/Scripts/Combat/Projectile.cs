using UnityEngine;

// Proyectil reutilizable: viaja en línea recta y daña a través de su Hitbox. Muere al
// recorrer su alcance (range) o al impactar, salvo que atraviese (pierce). Lo dispara el
// jugador; más adelante el boss usará el MISMO componente con balas parreables.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Hitbox))]
public class Projectile : MonoBehaviour
{
    private Rigidbody2D rb;
    private Hitbox hitbox;

    private Vector2 direction;
    private float speed;
    private float maxDistance;
    private bool pierce;
    private float traveled;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<Hitbox>();
    }

    // Lo llama quien dispara, justo después de instanciar la bala.
    public void Launch(Vector2 dir, float speed, float range, bool pierce, DamageInfo damage)
    {
        direction = dir.normalized;
        this.speed = speed;
        maxDistance = range;
        this.pierce = pierce;
        traveled = 0f;

        // Orienta la bala hacia su dirección (sprite mirando a la derecha por defecto).
        rb.SetRotation(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        hitbox.Hit += OnHit;
        hitbox.Activate(damage);
    }

    private void FixedUpdate()
    {
        float step = speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + direction * step);

        traveled += step;
        if (traveled >= maxDistance)
        {
            Despawn();
        }
    }

    private void OnHit(IDamageable target)
    {
        if (!pierce) Despawn();
    }

    private void Despawn()
    {
        if (hitbox != null) hitbox.Hit -= OnHit;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (hitbox != null) hitbox.Hit -= OnHit;
    }
}
