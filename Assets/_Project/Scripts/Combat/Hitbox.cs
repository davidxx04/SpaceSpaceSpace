using System.Collections.Generic;
using UnityEngine;

// Caja de golpeo reutilizable basada en colisión de tipo trigger. Detecta los
// IDamageable que entran (en las capas indicadas) y les aplica un DamageInfo.
// Se usa para el golpe melee del jugador (un hijo que se enciende durante el ataque)
// y, en el futuro, para proyectiles: el mismo componente sobre una bala que se mueve.
[RequireComponent(typeof(Collider2D))]
public class Hitbox : MonoBehaviour
{
    [Tooltip("Capas a las que esta hitbox puede dañar (p. ej. Enemy para el jugador).")]
    [SerializeField] private LayerMask targetLayers;

    private Collider2D col;
    private DamageInfo damage;
    private bool showDebug;
    private readonly HashSet<IDamageable> alreadyHit = new HashSet<IDamageable>();

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        col.isTrigger = true;
        col.enabled = false;   // inactiva hasta que un ataque la active
    }

    // Activa la hitbox con el golpe a aplicar. Limpia la lista de objetivos golpeados
    // para que cada activación dañe a cada objetivo una sola vez.
    public void Activate(DamageInfo info)
    {
        damage = info;
        alreadyHit.Clear();
        col.enabled = true;
    }

    public void Deactivate()
    {
        col.enabled = false;
    }

    // Ajusta el tamaño si la hitbox es una caja (lo usa el ataque para tunear el área).
    public void SetBoxSize(Vector2 size)
    {
        if (col is BoxCollider2D box) box.size = size;
    }

    // Activa/desactiva el dibujo de depuración de la caja (lo fija el ataque desde AttackData).
    public void SetDebug(bool value) => showDebug = value;

    // Dibuja la caja real (posición, rotación y escala) para depurar: gris en reposo,
    // verde mientras está golpeando (collider activado). Solo en el editor; no afecta a la build.
    private void OnDrawGizmos()
    {
        if (!showDebug) return;

        Collider2D c = col != null ? col : GetComponent<Collider2D>();
        if (c is BoxCollider2D box)
        {
            bool hitting = Application.isPlaying && c.enabled;
            Gizmos.color = hitting ? Color.green : Color.gray;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.offset, box.size);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Filtra por capa.
        if ((targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // El IDamageable puede estar en el propio collider o en un padre
        // (importante para las partes destruibles del boss).
        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || alreadyHit.Contains(target)) return;

        alreadyHit.Add(target);
        target.TakeDamage(damage);
    }
}
