using UnityEngine;

// Información de un golpe. Se pasa como struct para poder añadir campos en el futuro
// (empuje, tipo de daño, si es parreable...) sin tener que cambiar la firma de
// IDamageable en todos los sitios que la implementan.
public struct DamageInfo
{
    public float amount;       // daño aplicado
    public GameObject source;  // quién lo causó (jugador, boss...)
    public Vector2 direction;  // dirección del golpe (para empuje/efectos futuros)

    public DamageInfo(float amount, GameObject source, Vector2 direction)
    {
        this.amount = amount;
        this.source = source;
        this.direction = direction;
    }
}

// Contrato para cualquier cosa que pueda recibir daño. El jugador, el boss y cada
// parte destruible del boss serán IDamageable independientes.
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}
