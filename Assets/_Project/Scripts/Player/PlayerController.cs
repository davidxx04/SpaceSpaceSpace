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
    [SerializeField] private SpriteRenderer shipSprite;   // sprite de la nave (en un HIJO, no en la raíz)
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
    public float WalkSpeed => walkSpeed;

    // Última dirección de apuntado (normalizada). La usan el rol y el indicador.
    // Por defecto "arriba" para que un rol sin haber movido nunca quede a cero.
    public Vector2 AimDirection { get; private set; } = Vector2.up;

    // Lado actual de la nave (izquierda/derecha); solo se actualiza con intención horizontal real.
    private bool shipFacingLeft;

    // --- Recoil al disparar ---
    private Vector3 shipBaseLocalPos;   // posición local "en reposo" del sprite de la nave
    private Vector3 recoilOffset;       // desplazamiento visual actual del sprite (modo visual)
    private float recoilReturnSpeed;    // velocidad de recuperación del offset visual
    private Vector2 recoilBodyVel;      // velocidad de retroceso del cuerpo (modo físico)
    private float recoilBodyDamp;       // tiempo de amortiguación del retroceso físico

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

        if (shipSprite != null) shipBaseLocalPos = shipSprite.transform.localPosition;
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

        // Retroceso físico: se suma a la velocidad que haya fijado el estado y se amortigua.
        if (recoilBodyVel != Vector2.zero)
        {
            rb.velocity += recoilBodyVel;
            recoilBodyVel = Vector2.Lerp(recoilBodyVel, Vector2.zero, Time.fixedDeltaTime / recoilBodyDamp);
            if (recoilBodyVel.sqrMagnitude < 0.0001f) recoilBodyVel = Vector2.zero;
        }
    }

    // Mantiene la dirección de apuntado con el último input no nulo y orienta hacia ella el
    // indicador y la nave.
    private void UpdateAim()
    {
        Vector2 move = Input.MoveInput;
        if (move.sqrMagnitude > 0.01f)
        {
            AimDirection = move.normalized;
        }

        float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;

        // Indicador: su flecha apunta hacia "arriba" (+Y) por defecto.
        if (aimIndicator != null)
        {
            aimIndicator.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }

        // Nave: sprite lateral que mira a la DERECHA por defecto. Se rota hacia el apuntado y se
        // voltea en vertical (flipY) cuando mira a la izquierda, para que no quede "panza arriba".
        // El lado solo se actualiza con intención horizontal real, así al apuntar en vertical se
        // conserva el último lado (la nave mira "como si viniera" de ese lado).
        if (shipSprite != null)
        {
            shipSprite.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            if (Mathf.Abs(AimDirection.x) > 0.1f)
            {
                shipFacingLeft = AimDirection.x < 0f;
            }
            shipSprite.flipY = shipFacingLeft;
        }
    }

    // Recoil visual: el sprite "rebota" hacia atrás y vuelve a su posición de reposo.
    private void LateUpdate()
    {
        if (shipSprite == null) return;

        if (recoilOffset != Vector3.zero)
        {
            recoilOffset = Vector3.MoveTowards(recoilOffset, Vector3.zero, recoilReturnSpeed * Time.deltaTime);
        }
        shipSprite.transform.localPosition = shipBaseLocalPos + recoilOffset;
    }

    // Aplica el retroceso al disparar. 'dir' es el sentido del retroceso (opuesto al disparo).
    // Según recoilMovesPlayer mueve solo el sprite (visual) o empuja al jugador (físico).
    public void ApplyRecoil(Vector2 dir, AttackData data)
    {
        if (data.recoilDistance <= 0f) return;
        float recovery = Mathf.Max(0.01f, data.recoilRecovery);

        if (data.recoilMovesPlayer)
        {
            recoilBodyVel = dir * (data.recoilDistance / recovery);
            recoilBodyDamp = recovery;
        }
        else
        {
            recoilOffset = (Vector3)(dir * data.recoilDistance);
            recoilReturnSpeed = data.recoilDistance / recovery;
        }
    }
}
