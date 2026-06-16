using System;
using UnityEngine;

// Vida de una entidad. Implementa IDamageable. Mantiene la lógica numérica
// desacoplada de la presentación: la barra de vida / HUD se suscribe a los eventos
// en vez de leer este componente directamente.
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;

    // [field: SerializeField] expone el valor en el Inspector para poder verlo bajar
    // en vivo durante el Play (útil en prototipado).
    [field: SerializeField] public float Current { get; private set; }

    public float Max => maxHealth;
    public bool IsAlive => Current > 0f;

    // Se dispara con cada golpe recibido (para feedback: flash, números de daño...).
    public event Action<DamageInfo> Damaged;
    public event Action Died;

    private void Awake()
    {
        Current = maxHealth;
    }

    public void TakeDamage(DamageInfo info)
    {
        if (!IsAlive) return;

        Current = Mathf.Max(0f, Current - info.amount);
        Damaged?.Invoke(info);

        if (Current <= 0f)
        {
            Died?.Invoke();
        }
    }

    // Reinicia la vida (para pruebas o para la regeneración de partes del boss).
    public void ResetHealth()
    {
        Current = maxHealth;
    }
}
