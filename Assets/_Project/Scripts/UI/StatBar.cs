using UnityEngine;
using UnityEngine.UI;

// Barra rellenable genérica para el HUD (vida del player, energía, vida del boss). No conoce el
// dominio: solo recibe current/max y mueve el fillAmount de una Image (Image Type = Filled).
// Opcionalmente "persigue" el valor objetivo para dar juice (la barra baja/sube suave en vez de
// saltar). Un binder específico es quien la alimenta suscribiéndose a los eventos del modelo.
public class StatBar : MonoBehaviour
{
    [Tooltip("Image con Image Type = Filled; su fillAmount (0..1) representa el valor.")]
    [SerializeField] private Image fill;

    [Header("Suavizado (opcional)")]
    [Tooltip("Si está marcado, el relleno se interpola hacia el valor objetivo en vez de saltar.")]
    [SerializeField] private bool smooth = true;

    [Tooltip("Velocidad de persecución del relleno (fracción/seg). Mayor = más rápido.")]
    [SerializeField] private float lerpSpeed = 6f;

    private float target;     // fillAmount objetivo (0..1)
    private float displayed;  // fillAmount mostrado actualmente (0..1)
    private bool initialized; // ¿ya se ha fijado el primer valor? (el primero hace snap, no anima)

    // Fija el valor a representar. 'max' <= 0 se trata como barra vacía (evita dividir por cero).
    public void SetValue(float current, float max)
    {
        target = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        // El primer valor SIEMPRE se aplica de golpe (snap). Si no, una barra que arranca en su
        // valor por defecto (p.ej. energía a 0 = displayed 0) nunca escribiría el fillAmount y se
        // quedaría con el que tuviera la Image autorizada en escena (que estaba a 1 = llena).
        if (!smooth || !initialized)
        {
            initialized = true;
            displayed = target;
            Apply();
        }
    }

    private void Update()
    {
        if (!smooth) return;
        if (Mathf.Approximately(displayed, target)) return;

        displayed = Mathf.MoveTowards(displayed, target, lerpSpeed * Time.unscaledDeltaTime);
        Apply();
    }

    private void Apply()
    {
        if (fill != null) fill.fillAmount = displayed;
    }
}
