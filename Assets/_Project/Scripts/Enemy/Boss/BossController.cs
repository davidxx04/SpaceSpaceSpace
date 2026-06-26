using System;
using UnityEngine;

// Hub/contexto central del boss. Espejo de PlayerController: posee la máquina de estados de alto
// nivel (Intro/Combat/Stagger/Defeated), construye el BossContext que usan los ataques, conecta el
// Director, el movimiento y la vida (Health) del core, y gestiona la escalada por fases.
//
// Implementa IParryable: cuando el jugador parrea un ataque del boss, su DamageReceiver llama aquí
// (porque el 'source' del daño es este GameObject). Acumula "postura"; al pasar el umbral, el boss
// entra en Stagger (ventana de castigo). OJO: un parry NO corta el combo en marcha, para preservar
// las secuencias fijas que el jugador memoriza.
public class BossController : MonoBehaviour, IParryable
{
    [Header("Refs")]
    [SerializeField] private Health core;                   // vida principal del boss (independiente de las partes)
    [SerializeField] private BossAttackDirector director;
    [SerializeField] private BossMovement movement;         // opcional (se añade en su milestone)
    [SerializeField] private Transform[] muzzles;           // puntos de disparo (cañones); opcional
    [SerializeField] private Projectile bossProjectile;          // bala NO parreable (roja)
    [SerializeField] private Projectile parryableBossProjectile; // bala PARREABLE (cian); opcional

    [Header("Fases (ORDENADAS de más vida a menos: enterAtHealthFraction 1.0, 0.66, 0.33...)")]
    [SerializeField] private BossPhaseData[] phases;

    [Header("Tiempos / Postura")]
    [Tooltip("Duración de la intro antes de empezar a atacar, en segundos.")]
    [SerializeField] private float introSeconds = 1.5f;

    [Tooltip("Parries seguidos necesarios para romper la postura y entrar en Stagger.")]
    [SerializeField] private int staggerParryThreshold = 6;

    [Tooltip("Duración del Stagger (ventana de castigo), en segundos.")]
    [SerializeField] private float staggerSeconds = 2.5f;

    // --- Estado / acceso ---
    public Transform Player { get; private set; }
    public BossStateMachine StateMachine { get; private set; }
    public BossContext Context { get; private set; }
    public BossAttackDirector Director => director;
    public BossMovement Movement => movement;
    public Health Core => core;
    public BossPhaseData CurrentPhase { get; private set; }

    public float IntroSeconds => introSeconds;
    public float StaggerSeconds => staggerSeconds;
    public bool IsStaggered { get; set; }   // lo escribe BossStaggerState; lo pueden leer los VFX

    // Se dispara una sola vez al morir el boss (para la "coming soon card", parar música, etc.).
    public event Action Defeated;

    // Estados cacheados (sin asignaciones por transición).
    public BossIntroState IntroState { get; private set; }
    public BossCombatState CombatState { get; private set; }
    public BossStaggerState StaggerState { get; private set; }
    public BossDefeatedState DefeatedState { get; private set; }

    private int posture;

    private void Awake()
    {
        if (director == null) director = GetComponent<BossAttackDirector>();
        if (movement == null) movement = GetComponent<BossMovement>();

        var pc = FindObjectOfType<PlayerController>();
        Player = pc != null ? pc.transform : null;
        if (Player == null) Debug.LogWarning("[BossController] No se encontró PlayerController en la escena.");

        StateMachine = new BossStateMachine();
        IntroState = new BossIntroState(this);
        CombatState = new BossCombatState(this);
        StaggerState = new BossStaggerState(this);
        DefeatedState = new BossDefeatedState(this);

        Context = new BossContext
        {
            Boss = transform,
            Player = Player,
            Runner = director,
            Muzzles = muzzles,
            ProjectilePrefab = bossProjectile,
            ParryableProjectilePrefab = parryableBossProjectile,
            Movement = movement,
        };

        if (director != null) director.Initialize(Context);
        else Debug.LogError("[BossController] Falta BossAttackDirector.");

        if (movement != null) movement.SetPlayer(Player);

        if (core != null)
        {
            core.Damaged += OnCoreDamaged;
            core.Died += OnCoreDied;
        }
        else Debug.LogError("[BossController] Falta el Health del core.");
    }

    private void Start()
    {
        UpdatePhase();                            // fija la fase inicial (vida llena) y su movimiento
        StateMachine.ChangeState(IntroState);
    }

    private void OnDestroy()
    {
        if (core != null)
        {
            core.Damaged -= OnCoreDamaged;
            core.Died -= OnCoreDied;
        }
    }

    private void Update() => StateMachine.Tick();
    private void FixedUpdate() => StateMachine.FixedTick();

    // --- IParryable ---
    public void OnParried()
    {
        posture++;
        if (posture >= staggerParryThreshold && StateMachine.Current == CombatState)
        {
            StateMachine.ChangeState(StaggerState);
        }
    }

    public void ResetPosture() => posture = 0;

    // Aplica el movimiento de la fase actual (lo llaman UpdatePhase y CombatState al (re)entrar a combate).
    public void ApplyPhaseMovement()
    {
        if (movement == null || CurrentPhase == null) return;
        movement.SetData(CurrentPhase.movement);
        movement.SetBehavior(CurrentPhase.moveBehavior);
    }

    // Lo llama BossDefeatedState al entrar.
    public void NotifyDefeated() => Defeated?.Invoke();

    private void OnCoreDamaged(DamageInfo _) => UpdatePhase();

    private void OnCoreDied()
    {
        if (StateMachine.Current != DefeatedState)
            StateMachine.ChangeState(DefeatedState);
    }

    // Selecciona la fase más avanzada cuyo umbral ya se haya cruzado. DISPARADOR = vida del core;
    // cambiarlo a tiempo/postura/etc. es modificar solo este método.
    private void UpdatePhase()
    {
        if (phases == null || phases.Length == 0 || core == null) return;

        float frac = core.Max > 0f ? core.Current / core.Max : 0f;

        BossPhaseData chosen = phases[0];
        foreach (BossPhaseData p in phases)
        {
            if (p != null && frac <= p.enterAtHealthFraction) chosen = p;
        }

        if (chosen == CurrentPhase) return;       // sin cambios

        CurrentPhase = chosen;
        director?.SetPhase(chosen);
        ApplyPhaseMovement();
    }
}
