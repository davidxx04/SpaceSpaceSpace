using UnityEngine;

// Sacudida de cámara genérica. Llama a Shake(amplitude, duration) y la cámara tiembla con un
// offset aleatorio que decae, restaurando su posición base al terminar. Reutilizable (parry,
// golpes del boss...). Va en la cámara. Usa tiempo sin escalar para que el shake no dependa del
// timescale. Asume una cámara fija de arena (si más adelante sigue al jugador, el offset en
// LateUpdate se aplicaría sobre la posición ya calculada por el seguimiento).
public class CameraShake : MonoBehaviour
{
    private Vector3 basePos;
    private float amplitude;
    private float duration;
    private float elapsed;
    private bool shaking;

    public void Shake(float amplitude, float duration)
    {
        if (!shaking)
        {
            basePos = transform.localPosition;   // captura solo si no estábamos ya temblando
            shaking = true;
        }

        // El shake más fuerte/largo manda y se reinicia el temporizador.
        float remaining = this.duration - elapsed;
        this.amplitude = Mathf.Max(this.amplitude, amplitude);
        this.duration = Mathf.Max(remaining, duration);
        elapsed = 0f;
    }

    private void LateUpdate()
    {
        if (!shaking) return;

        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= duration)
        {
            transform.localPosition = basePos;
            shaking = false;
            amplitude = 0f;
            return;
        }

        float damper = 1f - Mathf.Clamp01(elapsed / duration);
        Vector2 offset = Random.insideUnitCircle * (amplitude * damper);
        transform.localPosition = basePos + (Vector3)offset;
    }
}
