using System;
using UnityEngine;

// Cuenta atrás de la partida (el tiempo que tiene el jugador para matar al boss). Cuando llega a 0
// dispara Expired una sola vez. Es un componente "tonto": no decide el resultado, solo mide tiempo.
// Lo arranca/para el MatchController, que reacciona a Expired (outcome "Survived").
public class MatchTimer : MonoBehaviour
{
    [Tooltip("Duración de la partida en segundos (360 = 6 min). Parametrizable.")]
    [SerializeField] private float matchSeconds = 360f;

    [Tooltip("Arrancar automáticamente en Start. Si lo controla el MatchController, déjalo desmarcado.")]
    [SerializeField] private bool autoStart = false;

    public float TimeRemaining { get; private set; }
    public float MatchSeconds => matchSeconds;
    public bool Running { get; private set; }

    // Se dispara una sola vez cuando la cuenta atrás llega a 0.
    public event Action Expired;

    private void Awake() => TimeRemaining = matchSeconds;

    private void Start()
    {
        if (autoStart) StartTimer();
    }

    // (Re)arranca la cuenta atrás desde matchSeconds.
    public void StartTimer()
    {
        TimeRemaining = matchSeconds;
        Running = true;
    }

    // Detiene la cuenta atrás (al terminar la partida por cualquier vía).
    public void Stop() => Running = false;

    private void Update()
    {
        if (!Running) return;

        TimeRemaining -= Time.deltaTime;
        if (TimeRemaining <= 0f)
        {
            TimeRemaining = 0f;
            Running = false;
            Expired?.Invoke();
        }
    }
}
