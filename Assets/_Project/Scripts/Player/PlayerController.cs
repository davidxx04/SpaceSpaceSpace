using UnityEngine;

// Hub/contexto central del jugador. Posee el lector de input y la máquina de estados,
// guarda las referencias compartidas y el estado en runtime que leen y escriben los
// estados individuales (Rigidbody, RollData, dirección de apuntado, invulnerabilidad,
// cooldown del rol).
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Transform aimIndicator;
    [SerializeField] private Hitbox attackHitbox;
    [SerializeField] private RollData rollData;
    [SerializeField] private AttackData attackData;
    [SerializeField] private ParryData parryData;

    [Header("Movimiento")]
    [Tooltip("Velocidad de caminado (lenta, estilo arcade).")]
    [SerializeField] private float walkSpeed = 2f;

    // --- Acceso para los estados ---
    public Rigidbody2D Rb => rb;
    public PlayerInputReader Input { get; private set; }
    public PlayerStateMachine StateMachine { get; private set; }
    public RollData RollData => rollData;
    public AttackData AttackData => attackData;
    public ParryData ParryData => parryData;
    public Hitbox AttackHitbox => attackHitbox;
    public float WalkSpeed => walkSpeed;

    // Última dirección de apuntado (normalizada). La usan el rol y el indicador.
    // Por defecto "arriba" para que un rol sin haber movido nunca quede a cero.
    public Vector2 AimDirection { get; private set; } = Vector2.up;

    // Activado por RollState durante la ventana de i-frames. El receptor de daño lo consulta.
    public bool IsInvulnerable { get; set; }

    // Activado por ParryState durante su ventana activa. El receptor de daño lo consulta.
    public bool IsParrying { get; set; }

    // Momento (Time.time) a partir del cual se puede volver a rodar.
    public float NextRollTime { get; set; }
    public bool CanRoll => Time.time >= NextRollTime;

    // Momento (Time.time) a partir del cual se puede volver a atacar.
    public float NextAttackTime { get; set; }
    public bool CanAttack => Time.time >= NextAttackTime;

    // Momento (Time.time) a partir del cual se puede volver a parrear.
    public float NextParryTime { get; set; }
    public bool CanParry => Time.time >= NextParryTime;

    // Estados cacheados (sin asignaciones de memoria por transición).
    public LocomotionState LocomotionState { get; private set; }
    public RollState RollState { get; private set; }
    public AttackState AttackState { get; private set; }
    public ParryState ParryState { get; private set; }

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        Input = new PlayerInputReader();
        StateMachine = new PlayerStateMachine();

        LocomotionState = new LocomotionState(this);
        RollState = new RollState(this);
        AttackState = new AttackState(this);
        ParryState = new ParryState(this);
    }

    private void OnEnable()
    {
        Input.Enable();
        Input.RollPerformed += OnRollInput;
        Input.AttackPerformed += OnAttackInput;
        Input.ParryPerformed += OnParryInput;
        StateMachine.ChangeState(LocomotionState);
    }

    private void OnDisable()
    {
        Input.RollPerformed -= OnRollInput;
        Input.AttackPerformed -= OnAttackInput;
        Input.ParryPerformed -= OnParryInput;
        Input.Disable();
    }

    private void OnDestroy()
    {
        Input.Dispose();
    }

    // Enrutado centralizado de los botones de acción. Un input solo prospera si el estado
    // actual permite interrumpirse (ventana de cancelación) y la acción está fuera de cooldown.
    private void OnRollInput()
    {
        if ((StateMachine.Current?.CanInterrupt ?? false) && CanRoll)
        {
            StateMachine.ChangeState(RollState);
        }
    }

    private void OnAttackInput()
    {
        if ((StateMachine.Current?.CanInterrupt ?? false) && CanAttack)
        {
            StateMachine.ChangeState(AttackState);
        }
    }

    private void OnParryInput()
    {
        if ((StateMachine.Current?.CanInterrupt ?? false) && CanParry)
        {
            StateMachine.ChangeState(ParryState);
        }
    }

    private void Update()
    {
        UpdateAim();
        StateMachine.Tick();
    }

    private void FixedUpdate()
    {
        StateMachine.FixedTick();
    }

    // Mantiene la dirección de apuntado con el último input no nulo y orienta el
    // indicador hacia ella.
    private void UpdateAim()
    {
        Vector2 move = Input.MoveInput;
        if (move.sqrMagnitude > 0.01f)
        {
            AimDirection = move.normalized;
        }

        if (aimIndicator != null)
        {
            float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;
            // Se asume que el sprite de la flecha apunta hacia "arriba" (+Y) por defecto.
            aimIndicator.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }
    }
}
