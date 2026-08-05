using System;
using UnityEngine;

// Un efecto de sonido con su mezcla: clip + volumen + recorte + anti-apilado + variación de tono.
// Es una CLASE serializable (no un asset por sonido) para que todos los sonidos vivan juntos en el
// SfxLibrary y se mezclen desde un único Inspector.
[Serializable]
public class SoundEffect
{
    public AudioClip clip;

    [Range(0f, 1f)]
    [Tooltip("Volumen de este sonido (se multiplica por el masterVolume de la librería).")]
    public float volume = 1f;

    [Tooltip("Salta los primeros N segundos del clip (para quitar un silencio inicial). 0 = desde el principio.")]
    public float startTime = 0f;

    [Tooltip("Recorta la reproducción a los primeros N segundos (contados desde startTime). 0 = hasta el final.")]
    public float maxDuration = 0f;

    [Tooltip("Fundido de salida en los últimos N segundos de la reproducción (evita el corte seco). " +
             "Solo aplica si maxDuration > 0. 0 = sin fundido.")]
    public float fadeOut = 0f;

    [Tooltip("Mínimo entre reproducciones del MISMO sonido (segundos). Evita apelmazar ráfagas " +
             "(p.ej. un abanico de balas que salen en el mismo frame suena una vez). 0 = sin límite.")]
    public float minInterval = 0f;

    [Tooltip("Rango (min,max) de tono aleatorio por reproducción. (1,1) = sin variación.")]
    public Vector2 pitchJitter = Vector2.one;

    // Runtime: último instante (realtime) en que sonó, para el throttle de minInterval.
    [NonSerialized] public float lastPlayTime = -999f;
}

// Librería central de SFX: el "mezclador" del juego. Un único asset (en Resources, para poder
// cargarlo desde cualquier escena) con todos los sonidos y sus volúmenes/recortes, editable en vivo
// durante el Play (los cambios en el asset persisten, como RollData).
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Audio/Sfx Library", fileName = "SfxLibrary")]
public class SfxLibrary : ScriptableObject
{
    [Header("Boss")]
    public SoundEffect bossBullet;
    public SoundEffect bossSwoosh;
    public SoundEffect bossArea;

    [Header("Jugador")]
    public SoundEffect playerShoot;
    public SoundEffect dash;
    public SoundEffect parry;
    public SoundEffect parrySuccess;
    public SoundEffect playerHurt;

    [Header("UI")]
    public SoundEffect uiClick;
    public SoundEffect uiHover;
    public SoundEffect gameOver;

    [Header("Global")]
    [Range(0f, 1f)]
    [Tooltip("Volumen maestro de todos los SFX (fader general).")]
    public float masterVolume = 1f;
}
