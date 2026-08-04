using System;
using UnityEngine;

// Coordinador del resultado de UNA partida (por-run). El GameManager es de sesión
// (DontDestroyOnLoad) y solo enruta escenas; esto vive en la escena Game y centraliza el final:
// arranca el reloj y escucha las tres vías de fin de partida, calcula el score y emite un único
// MatchEnded. Las pantallas reales (death / victory / survived) se suscribirán a ese evento; de
// momento solo se loguea (placeholder).
//
//   boss derrotado        -> Victory  (score completo + bonus de tiempo restante)
//   jugador muerto        -> Death    (score reducido por el daño infligido)
//   reloj a 0 (boss vivo) -> Survived (NO es game over; score intermedio)
public class MatchController : MonoBehaviour
{
    public enum MatchOutcome { Victory, Death, Survived }

    public readonly struct MatchResult
    {
        public readonly MatchOutcome Outcome;
        public readonly int Score;
        public readonly float TimeRemaining;

        public MatchResult(MatchOutcome outcome, int score, float timeRemaining)
        {
            Outcome = outcome;
            Score = score;
            TimeRemaining = timeRemaining;
        }
    }

    [Header("Refs")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private PlayerDamageReceiver playerReceiver;   // fuente de parries/golpes para el score en vivo
    [SerializeField] private BossController boss;
    [SerializeField] private MatchTimer timer;

    [Header("Score (placeholder tuneable)")]
    [Tooltip("Puntos por cada punto de vida arrancado al core del boss.")]
    [SerializeField] private float scorePerBossDamage = 1f;

    [Tooltip("Puntos por segundo restante en el reloj (solo en Victory).")]
    [SerializeField] private float timeBonusPerSecond = 10f;

    [Tooltip("Puntos sumados por cada parry acertado.")]
    [SerializeField] private float scorePerParry = 50f;

    [Tooltip("Puntos restados por cada golpe recibido.")]
    [SerializeField] private float scorePenaltyPerHit = 100f;

    [Tooltip("Multiplicadores de score por resultado (Victory > Survived > Death).")]
    [SerializeField] private float victoryMultiplier = 1f;
    [SerializeField] private float survivedMultiplier = 0.6f;
    [SerializeField] private float deathMultiplier = 0.3f;

    // Score acumulado en vivo durante la partida: +parry, -golpe. Se suma al score final.
    private float runningScore;

    // Se dispara una sola vez al terminar la partida (lo consumirán las pantallas finales).
    public event Action<MatchResult> MatchEnded;

    public bool IsOver { get; private set; }
    public MatchResult Result { get; private set; }

    private void Start()
    {
        // Resuelve el receptor desde el mismo GameObject que el Health del player (mismo objeto raíz).
        if (playerReceiver == null && playerHealth != null)
            playerReceiver = playerHealth.GetComponent<PlayerDamageReceiver>();

        if (timer != null) timer.StartTimer();   // arranca el reloj de la run

        if (boss != null) boss.Defeated += OnBossDefeated;
        if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        if (timer != null) timer.Expired += OnTimerExpired;
        if (playerReceiver != null)
        {
            playerReceiver.ParrySuccess += OnParrySuccess;
            playerReceiver.Hit += OnPlayerHit;
        }
    }

    private void OnDestroy()
    {
        if (boss != null) boss.Defeated -= OnBossDefeated;
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        if (timer != null) timer.Expired -= OnTimerExpired;
        if (playerReceiver != null)
        {
            playerReceiver.ParrySuccess -= OnParrySuccess;
            playerReceiver.Hit -= OnPlayerHit;
        }
    }

    private void OnBossDefeated() => EndMatch(MatchOutcome.Victory);
    private void OnPlayerDied() => EndMatch(MatchOutcome.Death);
    private void OnTimerExpired() => EndMatch(MatchOutcome.Survived);

    // Score en vivo: cada parry acertado suma, cada golpe recibido resta (ignorados tras el final).
    private void OnParrySuccess(DamageInfo _)
    {
        if (!IsOver) runningScore += scorePerParry;
    }

    private void OnPlayerHit(DamageInfo _)
    {
        if (!IsOver) runningScore -= scorePenaltyPerHit;
    }

    // El primer resultado gana; los demás se ignoran (la partida ya terminó).
    private void EndMatch(MatchOutcome outcome)
    {
        if (IsOver) return;
        IsOver = true;

        if (timer != null) timer.Stop();

        // Congela el mundo detrás de la pantalla final (en Survived el boss seguiría atacando). Es
        // seguro: el GameManager restaura timeScale = 1 al cargar escena, la UI usa unscaledDeltaTime
        // y el input/uGUI siguen respondiendo con timeScale = 0. La pantalla queda pura vista.
        Time.timeScale = 0f;

        float timeRemaining = timer != null ? timer.TimeRemaining : 0f;
        int score = ComputeScore(outcome, timeRemaining);

        Result = new MatchResult(outcome, score, timeRemaining);
        Debug.Log($"[Match] {outcome} — score: {score} (tiempo restante: {timeRemaining:0}s)");

        // Jingle de "game over" solo al PERDER (muerte). Suena aunque timeScale=0 (AudioManager usa DSP).
        if (outcome == MatchOutcome.Death) AudioManager.PlayGameOver();

        MatchEnded?.Invoke(Result);
    }

    private int ComputeScore(MatchOutcome outcome, float timeRemaining)
    {
        // Daño infligido al core del boss (en Victory equivale a su vida completa).
        float bossDamage = 0f;
        if (boss != null && boss.Core != null)
        {
            bossDamage = Mathf.Max(0f, boss.Core.Max - boss.Core.Current);
        }

        float multiplier = outcome switch
        {
            MatchOutcome.Victory => victoryMultiplier,
            MatchOutcome.Survived => survivedMultiplier,
            _ => deathMultiplier,
        };

        float score = bossDamage * scorePerBossDamage * multiplier;
        if (outcome == MatchOutcome.Victory) score += timeRemaining * timeBonusPerSecond;

        score += runningScore;   // acumulado de parries (+) y golpes (-) durante la partida

        return Mathf.Max(0, Mathf.RoundToInt(score));
    }
}
