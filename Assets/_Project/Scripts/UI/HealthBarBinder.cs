using UnityEngine;

// Glue genérico HUD <- Health. Conecta un Health (player o core del boss) con un StatBar:
// se suscribe a Health.Damaged y fija el valor inicial en Start. La barra no conoce el combate;
// solo escucha el evento, siguiendo el patrón del proyecto (la UI se suscribe, no lee estado).
public class HealthBarBinder : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private StatBar bar;

    private void Start()
    {
        // Valor inicial (Health ya tiene Current = Max tras su Awake).
        Refresh();
    }

    private void OnEnable()
    {
        if (health != null) health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }

    private void OnDamaged(DamageInfo _) => Refresh();

    private void Refresh()
    {
        if (health != null && bar != null) bar.SetValue(health.Current, health.Max);
    }
}
