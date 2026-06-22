using System;
using UnityEngine;

// Animador de sprites por código (flipbook): cicla un conjunto de fotogramas sobre un
// SpriteRenderer a una cadencia configurable. Es GENÉRICO y reutilizable (propulsor de la
// nave, fogonazo del disparo, explosiones, FX del boss...): no sabe nada del juego; quien lo
// usa le pasa los fotogramas y la velocidad.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFlipbook : MonoBehaviour
{
    [Tooltip("Fotogramas de la animación, en orden.")]
    [SerializeField] private Sprite[] frames;

    [Min(0f)]
    [Tooltip("Velocidad de la animación en fotogramas por segundo.")]
    [SerializeField] private float framesPerSecond = 12f;

    [Tooltip("Empieza a reproducir automáticamente al habilitarse.")]
    [SerializeField] private bool playOnEnable = true;

    [Tooltip("Repite la animación en bucle. Si se desmarca, se reproduce una sola vez y se detiene.")]
    [SerializeField] private bool loop = true;

    [Tooltip("Ida y vuelta: al llegar al último fotograma recorre la animación a la inversa hasta el primero (\"que vuelva\").")]
    [SerializeField] private bool pingPong = false;

    [Tooltip("Segundos que se mantiene el último fotograma antes de iniciar la vuelta (solo pingPong). 0 = sin pausa.")]
    [SerializeField, Min(0f)] private float pingPongHoldSeconds = 0f;

    [Tooltip("Al terminar (solo si NO hay bucle), desactiva su propio GameObject. Útil para FX de un disparo: golpe, explosión...")]
    [SerializeField] private bool disableObjectOnFinish = false;

    // Se dispara al terminar una animación sin bucle (tras la ida-y-vuelta si es pingPong).
    public event Action Finished;

    private SpriteRenderer sr;
    private int index;
    private int direction = 1;   // sentido de avance (1 = ida, -1 = vuelta) para pingPong
    private float timer;
    private bool playing;
    private bool holding;        // pausa manteniendo el apex del pingPong
    private float holdTimer;

    // Velocidad ajustable en runtime (la usa ShipThruster para flight/turbo).
    public float FramesPerSecond
    {
        get => framesPerSecond;
        set => framesPerSecond = Mathf.Max(0f, value);
    }

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    private void OnEnable()
    {
        if (playOnEnable) Play();
    }

    // Reinicia desde el principio y reproduce. Para one-shots, basta con reactivar el GameObject
    // (OnEnable llama aquí), de modo que un golpe seguido vuelve a arrancar la animación limpia.
    public void Play()
    {
        index = 0;
        direction = 1;
        timer = 0f;
        holding = false;
        holdTimer = 0f;
        playing = true;
        ShowCurrent();
    }

    public void Stop() => playing = false;

    // Cambia el set de fotogramas (p. ej. flight <-> turbo). Si es el MISMO array no reinicia,
    // para poder llamarse cada frame sin cortar el bucle.
    public void SetFrames(Sprite[] newFrames, bool restart = false)
    {
        if (frames == newFrames && !restart) return;

        frames = newFrames;
        if (restart)
        {
            index = 0;
            timer = 0f;
        }
        ShowCurrent();
    }

    private void Update()
    {
        if (!playing || frames == null || frames.Length == 0 || framesPerSecond <= 0f) return;

        float dt = Time.deltaTime;

        // Pausa en el apex del pingPong: mantenemos el último fotograma unos segundos antes de volver.
        if (holding)
        {
            holdTimer -= dt;
            if (holdTimer > 0f) return;
            holding = false;
            timer = 0f;
        }

        float frameDuration = 1f / framesPerSecond;
        timer += dt;
        while (playing && !holding && timer >= frameDuration)
        {
            timer -= frameDuration;
            Advance();
            sr.sprite = frames[index];
        }
    }

    // Avanza un fotograma respetando loop/pingPong; al cerrar un ciclo sin bucle, termina.
    private void Advance()
    {
        int len = frames.Length;
        if (len <= 1) { index = 0; if (!loop) Finish(); return; }

        if (pingPong)
        {
            index += direction;
            if (index > len - 1)            // tocó el final -> (opcional) mantener y luego volver
            {
                direction = -1;
                if (pingPongHoldSeconds > 0f)
                {
                    index = len - 1;        // mantener el apex visible durante la pausa
                    holding = true;
                    holdTimer = pingPongHoldSeconds;
                }
                else
                {
                    index = len - 2;        // sin pausa: rebota directo
                }
            }
            else if (index < 0)             // volvió al principio -> fin del ciclo ida-y-vuelta
            {
                if (loop) { index = 1; direction = 1; }
                else { index = 0; Finish(); }
            }
        }
        else
        {
            index++;
            if (index > len - 1)
            {
                if (loop) index = 0;
                else { index = len - 1; Finish(); }
            }
        }
    }

    private void Finish()
    {
        playing = false;
        Finished?.Invoke();
        if (disableObjectOnFinish) gameObject.SetActive(false);
    }

    // Asigna el fotograma actual sin avanzar (defensivo ante arrays de distinta longitud).
    private void ShowCurrent()
    {
        if (frames != null && frames.Length > 0)
        {
            sr.sprite = frames[index % frames.Length];
        }
    }
}
