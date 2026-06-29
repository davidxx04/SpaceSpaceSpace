using System;
using UnityEngine;

// Recurso de energía del jugador. Se carga al ACERTAR parries (reutiliza el evento
// PlayerDamageReceiver.ParrySuccess, no crea uno nuevo) y se gasta al disparar el ataque
// especial. Sigue el patrón del proyecto: la lógica vive aquí; la barra de UI se suscribe a
// EnergyChanged en vez de leer este componente directamente (igual que Health.Damaged).
//
// Quién decide qué arma sale por el botón de ataque es el PlayerController; este componente solo
// expone el estado del recurso (CanFireSpecial / IsFull) y los métodos para gastarlo.
public class EnergyTank : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Fuente de la energía: cada parry acertado carga la barra.")]
    [SerializeField] private PlayerDamageReceiver receiver;

    [Header("Valores")]
    [Tooltip("Capacidad máxima de la barra.")]
    [SerializeField] private float maxEnergy = 100f;

    [Tooltip("Energía con la que arranca la run (0 = barra vacía).")]
    [SerializeField] private float startingEnergy = 0f;

    [Tooltip("Energía ganada por cada parry acertado.")]
    [SerializeField] private float gainPerParry = 10f;

    [Tooltip("Energía consumida por cada disparo del ataque especial.")]
    [SerializeField] private float specialCost = 5f;

    // [field: SerializeField] expone el valor en el Inspector para verlo subir/bajar en vivo.
    [field: SerializeField] public float Current { get; private set; }

    public float Max => maxEnergy;
    public float SpecialCost => specialCost;

    // ¿Hay energía suficiente para un disparo especial?
    public bool CanFireSpecial => Current >= specialCost;
    // ¿Barra al completo? (gatillo del futuro "nuke").
    public bool IsFull => Current >= maxEnergy;

    // Se dispara en cada cambio de energía (carga o gasto). La barra de UI se suscribe aquí.
    public event Action EnergyChanged;

    private void Awake()
    {
        if (receiver == null) receiver = GetComponentInParent<PlayerDamageReceiver>();
        Current = Mathf.Clamp(startingEnergy, 0f, maxEnergy);
    }

    private void OnEnable()
    {
        if (receiver != null) receiver.ParrySuccess += OnParrySuccess;
    }

    private void OnDisable()
    {
        if (receiver != null) receiver.ParrySuccess -= OnParrySuccess;
    }

    private void OnParrySuccess(DamageInfo _) => Add(gainPerParry);

    public void Add(float amount)
    {
        if (amount == 0f) return;
        Current = Mathf.Clamp(Current + amount, 0f, maxEnergy);
        EnergyChanged?.Invoke();
    }

    // Gasta 'amount' (clampado a lo disponible). Lo llama el PlayerController al disparar.
    public void Spend(float amount)
    {
        if (amount <= 0f) return;
        Current = Mathf.Max(0f, Current - amount);
        EnergyChanged?.Invoke();
    }

    // Vacía la barra de golpe (consumo del "nuke" a barra llena).
    public void ConsumeAll()
    {
        if (Current == 0f) return;
        Current = 0f;
        EnergyChanged?.Invoke();
    }
}
