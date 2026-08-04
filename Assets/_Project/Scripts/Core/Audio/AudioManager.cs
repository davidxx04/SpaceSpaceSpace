using System.Collections;
using UnityEngine;

// Reproductor de SFX del juego. Singleton PEREZOSO con API estática (mismo espíritu que PoolManager):
// nadie lo coloca en escena; se autocrea en el primer uso, persiste (DontDestroyOnLoad) y carga la
// mezcla desde Resources/SfxLibrary. Un pool de AudioSource 2D en round-robin reproduce los clips.
//
// Recorte SIN tocar el archivo: PlayScheduled + SetScheduledEndTime en el reloj DSP (independiente de
// Time.timeScale), así el game-over suena aunque la partida esté congelada (timeScale = 0). El
// throttle de minInterval usa realtime (también inmune al timeScale).
public class AudioManager : MonoBehaviour
{
    private const int VoiceCount = 16;              // AudioSources simultáneos (de sobra para SFX)
    private const string LibraryResource = "SfxLibrary";

    private static AudioManager instance;
    private static bool quitting;

    // Acceso perezoso: crea el manager la primera vez que se pide (y nunca durante el cierre del juego).
    private static AudioManager Instance
    {
        get
        {
            if (instance == null && !quitting)
            {
                var go = new GameObject("AudioManager");
                instance = go.AddComponent<AudioManager>();
            }
            return instance;
        }
    }

    private SfxLibrary library;
    private AudioSource[] voices;
    private Coroutine[] fades;   // corrutina de recorte/fade en curso por voz (para cancelar al reutilizar)
    private int nextVoice;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);

        library = Resources.Load<SfxLibrary>(LibraryResource);
        if (library == null)
            Debug.LogWarning($"[AudioManager] No se encontró Resources/{LibraryResource}. No sonará nada.");

        voices = new AudioSource[VoiceCount];
        fades = new Coroutine[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;   // 2D: sin posición (arcade a pantalla)
            src.loop = false;
            voices[i] = src;
        }
    }

    private void OnApplicationQuit() => quitting = true;

    // --- API estática por sonido (llamadas de una línea desde los enganches) ---
    public static void PlayBossBullet()  => Play(Lib?.bossBullet);
    public static void PlayBossSwoosh()  => Play(Lib?.bossSwoosh);
    public static void PlayBossArea()    => Play(Lib?.bossArea);
    public static void PlayPlayerShoot() => Play(Lib?.playerShoot);
    public static void PlayDash()         => Play(Lib?.dash);
    public static void PlayParry()        => Play(Lib?.parry);
    public static void PlayParrySuccess() => Play(Lib?.parrySuccess);
    public static void PlayUiClick()     => Play(Lib?.uiClick);
    public static void PlayUiHover()     => Play(Lib?.uiHover);
    public static void PlayGameOver()    => Play(Lib?.gameOver);

    private static SfxLibrary Lib => Instance != null ? Instance.library : null;

    // Reproduce un SoundEffect (respeta volumen/recorte/throttle/pitch). Seguro con null.
    public static void Play(SoundEffect sfx)
    {
        if (sfx == null || sfx.clip == null) return;
        var mgr = Instance;
        if (mgr != null) mgr.PlayInternal(sfx);
    }

    private void PlayInternal(SoundEffect sfx)
    {
        // Anti-apilado: no re-suena el mismo efecto antes de minInterval (realtime = inmune a timeScale).
        float now = Time.realtimeSinceStartup;
        if (sfx.minInterval > 0f && now - sfx.lastPlayTime < sfx.minInterval) return;
        sfx.lastPlayTime = now;

        int i = nextVoice;
        nextVoice = (nextVoice + 1) % voices.Length;
        AudioSource src = voices[i];

        // Cancela un recorte/fade anterior de esta voz (por si se reutiliza a media reproducción).
        if (fades[i] != null) { StopCoroutine(fades[i]); fades[i] = null; }

        src.Stop();
        src.clip = sfx.clip;
        float master = library != null ? library.masterVolume : 1f;
        float baseVol = Mathf.Clamp01(sfx.volume * master);
        src.volume = baseVol;
        src.pitch = (sfx.pitchJitter.x < sfx.pitchJitter.y)
            ? Random.Range(sfx.pitchJitter.x, sfx.pitchJitter.y)
            : Mathf.Max(0.01f, sfx.pitchJitter.x);

        // Salta el silencio inicial del clip (seek limpio en clips Decompress On Load).
        if (sfx.startTime > 0f)
            src.time = Mathf.Clamp(sfx.startTime, 0f, Mathf.Max(0f, sfx.clip.length - 0.01f));

        src.Play();

        // Recorte y/o fade con corrutina en realtime (funciona a timeScale = 0). Si hay maxDuration se
        // recorta a eso; si solo hay fadeOut, se hace una cola de fundido al final del clip natural.
        float effectiveDur = sfx.maxDuration > 0f
            ? sfx.maxDuration
            : (sfx.fadeOut > 0f ? Mathf.Max(0f, sfx.clip.length - Mathf.Max(0f, sfx.startTime)) : 0f);
        if (effectiveDur > 0f)
            fades[i] = StartCoroutine(TrimAndFade(i, src, baseVol, effectiveDur, sfx.fadeOut));
    }

    // Deja sonar 'duration' s y, en los últimos 'fade' s, baja el volumen a 0; luego para y restaura.
    private IEnumerator TrimAndFade(int voiceIndex, AudioSource src, float baseVol, float duration, float fade)
    {
        fade = Mathf.Clamp(fade, 0f, duration);
        float steady = duration - fade;

        float t = 0f;
        while (t < steady && src.isPlaying)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < fade && src.isPlaying)
        {
            t += Time.unscaledDeltaTime;
            src.volume = baseVol * Mathf.Clamp01(1f - t / fade);
            yield return null;
        }

        src.Stop();
        src.volume = baseVol;   // restaura para la próxima reutilización de la voz
        fades[voiceIndex] = null;
    }
}
