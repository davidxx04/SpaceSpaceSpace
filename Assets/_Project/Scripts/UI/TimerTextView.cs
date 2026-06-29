using TMPro;
using UnityEngine;

// Vista del HUD para el MatchTimer: formatea TimeRemaining como mm:ss en un TMP_Text.
// Solo lee el tiempo restante; no decide nada de la partida.
public class TimerTextView : MonoBehaviour
{
    [SerializeField] private MatchTimer timer;
    [SerializeField] private TMP_Text label;

    private int lastShownSeconds = -1;

    private void Update()
    {
        if (timer == null || label == null) return;

        // Redondea hacia arriba para que el reloj marque "6:00" durante el primer segundo.
        int seconds = Mathf.CeilToInt(timer.TimeRemaining);
        if (seconds == lastShownSeconds) return;   // solo redibuja al cambiar de segundo

        lastShownSeconds = seconds;
        label.text = $"{seconds / 60}:{seconds % 60:00}";
    }
}
