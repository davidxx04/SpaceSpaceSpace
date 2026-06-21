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

    private SpriteRenderer sr;
    private int index;
    private float timer;
    private bool playing;

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

    public void Play()
    {
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

        float frameDuration = 1f / framesPerSecond;
        timer += Time.deltaTime;
        while (timer >= frameDuration)
        {
            timer -= frameDuration;
            index = (index + 1) % frames.Length;
            sr.sprite = frames[index];
        }
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
