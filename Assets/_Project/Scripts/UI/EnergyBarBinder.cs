using UnityEngine;

// Glue genérico HUD <- EnergyTank. Conecta el recurso de energía con un StatBar: se suscribe a
// EnergyChanged y fija el valor inicial en Start. Mismo patrón que HealthBarBinder.
public class EnergyBarBinder : MonoBehaviour
{
    [SerializeField] private EnergyTank tank;
    [SerializeField] private StatBar bar;

    private void Start() => Refresh();

    private void OnEnable()
    {
        if (tank != null) tank.EnergyChanged += Refresh;
    }

    private void OnDisable()
    {
        if (tank != null) tank.EnergyChanged -= Refresh;
    }

    private void Refresh()
    {
        if (tank != null && bar != null) bar.SetValue(tank.Current, tank.Max);
    }
}
