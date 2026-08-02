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
    [SerializeField] private AfterimageEmitter afterimage; // emisor de estelas del dash (opcional)
    [SerializeField] private RollData rollData;
    [SerializeField] private AttackData attackData;
    [Tooltip("Ataque especial (gasta energía). Sale por el MISMO botón de ataque cuando hay energía suficiente.")]
    [SerializeField] private AttackData specialAttackData;
    [Tooltip("Ataque 'nuke' a barra de energía LLENA (opcional). Vacío = nunca se dispara (queda la estructura lista).")]
    [SerializeField] private AttackData nukeAttackData;
    [SerializeField] private ParryData parryData;

    [Header("Recursos")]
    [SerializeField] private EnergyTank energy;

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

    // Arma resuelta para el disparo en curso (normal / especial / nuke). La lee AttackState en lugar
    // de AttackData fijo, para que cooldown/cadencia/patrón sigan al arma que de verdad se dispara.
    // La elige TryStartAttack según la energía; por defecto es el ataque normal.
    public AttackData CurrentAttackData { get; private set; }

    // Coste de energía del disparo en curso (resuelto junto con CurrentAttackData). Lo consume
    // AttackState al disparar (NotifyAttackFired), no al entrar al estado.
    private float pendingEnergyCost;
    private bool pendingConsumeAll;   // true si el disparo en curso es el "nuke" (vacía la barra entera)

    // Última dirección de apuntado (normalizada). La usan el rol y el indicador.
    // Por defecto "arriba" para que un rol sin haber movido nunca quede a cero.
    public Vector2 AimDirection { get; private set; } = Vector2.up;

    // Lado actual de la nave (izquierda/derecha); solo se actualiza con intención horizontal real.
    private bool shipFacingLeft;

    // --- Giro visual de la nave durante el parry (flourish) ---
    private bool shipSpinning;         // true mientras dura la acción de parry (ParryState.Enter/Exit)
    private float shipSpinAngle;       // ángulo libre acumulado mientras gira (sin envolver)
    private bool shipStabilizing;      // true mientras se asienta hacia el aim real tras el giro
    private float stabilizeElapsed;
    private float stabilizeFromAngle;

    // --- Recoil al disparar ---
    private Vector3 shipBaseLocalPos;   // posición local "en reposo" del sprite de la nave
    private Vector3 recoilOffset;       // desplazamiento visual actual del sprite (modo visual)
    private float recoilReturnSpeed;    // velocidad de recuperación del offset visual
    private Vector2 recoilBodyVel;      // velocidad de retroceso del cuerpo (modo físico)
    private float recoilBodyDamp;       // tiempo de amortiguación del retroceso físico

    // Invulnerabilidad que consulta el receptor de daño. Es la SUMA de varias fuentes:
    //  - la ventana de i-frames del rol (la escribe RollState con el setter),
    //  - una ventana temporal concedida por GrantInvulnerability (gracia tras recibir un golpe, etc.).
    private bool rollInvulnerable;
    private float invulnerableUntil;
    public bool IsInvulnerable
    {
        get => rollInvulnerable || Time.time < invulnerableUntil;
        set => rollInvulnerable = value;   // RollState sigue escribiendo su ventana aquí
    }

    // Concede invulnerabilidad temporal (i-frames de cortesía tras un golpe, etc.). No pisa el rol;
    // si se solapan, se queda con la ventana que termine más tarde.
    public void GrantInvulnerability(float seconds)
    {
        if (seconds <= 0f) return;
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
    }

    // Disparo automático: cuando el jugador rueda/parrea MIENTRAS mantiene pulsado el botón de ataque,
    // se "consume" ese mantenido para que el auto-fire NO se reanude solo al abrirse la cancel window
    // (se sentiría como un bug). Para reanudar hay que SOLTAR y volver a pulsar K (ya es intención clara).
    private bool attackHoldConsumed;

    // Activado por ParryState durante su ventana activa. El receptor de daño lo consulta.
    public bool IsParrying { get; set; }

    // Progreso dentro de la ventana de parry (0 al abrirse, 1 al cerrarse). Lo escribe ParryState
    // y lo lee el VFX del aura para encoger el anillo de timing.
    public float ParryWindowProgress { get; set; }

    // Estado de lectura para el feedback visual (p. ej. el propulsor), expuesto como API
    // pública para no acoplar esos componentes a los internos de la FSM.
    public bool IsRolling => StateMachine.Current == RollState;
    public bool ShipFacingLeft => shipFacingLeft;

    // Emisor de estelas del dash (lo usan los estados de rol/dash para arrancar/parar el rastro).
    public AfterimageEmitter Afterimage => afterimage;

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
        if (afterimage == null) afterimage = GetComponent<AfterimageEmitter>();

        CurrentAttackData = attackData;   // arma por defecto hasta que TryStartAttack resuelva otra

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
            attackHoldConsumed = true;   // consume el mantenido de K: el auto-fire no se reanuda solo tras el roll
        }
    }

    private void OnAttackInput() => TryStartAttack();

    // Inicia el ataque si el estado actual permite interrumpirse y está fuera de cooldown.
    // Lo llaman el input de ataque (al pulsar) y el disparo automático (mientras se mantiene).
    private void TryStartAttack()
    {
        if ((StateMachine.Current?.CanInterrupt ?? false) && CanAttack)
        {
            ResolveAttack();
            StateMachine.ChangeState(AttackState);
        }
    }

    // Elige qué arma sale por el botón de ataque según la energía disponible (mismo botón para
    // normal/especial/nuke, porque la cabina solo tiene 3 botones). Guarda el coste para cobrarlo
    // en el disparo real (NotifyAttackFired), no aquí.
    private void ResolveAttack()
    {
        pendingEnergyCost = 0f;
        pendingConsumeAll = false;

        if (nukeAttackData != null && energy != null && energy.IsFull)
        {
            CurrentAttackData = nukeAttackData;
            pendingConsumeAll = true;            // el nuke vacía la barra entera
        }
        else if (specialAttackData != null && energy != null && energy.CanFireSpecial)
        {
            CurrentAttackData = specialAttackData;
            pendingEnergyCost = energy.SpecialCost;
        }
        else
        {
            CurrentAttackData = attackData;
        }
    }

    // Lo llama AttackState cuando el disparo SALE de verdad: cobra la energía del arma resuelta.
    public void NotifyAttackFired()
    {
        if (energy == null) return;
        if (pendingConsumeAll) energy.ConsumeAll();
        else if (pendingEnergyCost > 0f) energy.Spend(pendingEnergyCost);

        pendingEnergyCost = 0f;
        pendingConsumeAll = false;
    }

    private void OnParryInput()
    {
        if ((StateMachine.Current?.CanInterrupt ?? false) && CanParry)
        {
            StateMachine.ChangeState(ParryState);
            attackHoldConsumed = true;   // mismo criterio que el roll: parrear con K mantenida no reanuda el auto-fire
        }
    }

    // Lo llama PlayerDamageReceiver al confirmar un parry acertado. Reenvía la señal al ParryState
    // activo (que habilita el encadenado y cancela el cooldown del parry para re-parrear al instante).
    public void NotifyParrySuccess()
    {
        if (StateMachine.Current == ParryState) ParryState.RegisterSuccess();
    }

    // Arranca el giro visual (flourish) de la nave durante el parry. Lo llama ParryState.Enter().
    // Semilla el ángulo libre desde el que ya se está renderizando (spin o aim real, da igual) para
    // que el giro nunca dé un salto, ni siquiera al encadenar un re-parry (Exit()+Enter() ocurren en
    // el mismo frame, antes de que UpdateAim vuelva a leer el ángulo).
    public void BeginShipSpin()
    {
        shipStabilizing = false;
        shipSpinning = true;
        if (shipSprite != null) shipSpinAngle = shipSprite.transform.eulerAngles.z;
    }

    // Termina el giro y arranca el asentamiento suave hacia el aim real. Lo llama ParryState.Exit(),
    // que se dispara tanto si el parry termina solo como si se cancela por otra acción.
    public void EndShipSpin()
    {
        shipSpinning = false;
        shipStabilizing = true;
        stabilizeElapsed = 0f;
        stabilizeFromAngle = shipSpinAngle;
    }

    // Lo llama PlayerDamageReceiver al recibir daño REAL. Si el arma en curso lo permite
    // (interruptOnHit), cancela el disparo (vuelve a locomoción) para que no puedas spamear
    // disparos estáticos encajando golpes. Consume el mantenido para que el auto-fire no reanude solo.
    public void NotifyDamaged()
    {
        if (StateMachine.Current == AttackState && CurrentAttackData != null && CurrentAttackData.interruptOnHit)
        {
            StateMachine.ChangeState(LocomotionState);
            attackHoldConsumed = true;
        }
    }

    private void Update()
    {
        UpdateAim();
        StateMachine.Tick();

        // Al soltar el botón de ataque se "rearma": el siguiente mantenido vuelve a poder auto-disparar.
        if (!Input.AttackHeld) attackHoldConsumed = false;

        // Disparo automático: mientras se MANTIENE pulsado, el AttackData lo permite y el mantenido no se ha
        // consumido por un roll/parry previo (gated además por el cooldown).
        if (attackData != null && attackData.automatic && Input.AttackHeld && !attackHoldConsumed) TryStartAttack();
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
            float shipAngle = ResolveShipAngle(angle);
            shipSprite.transform.rotation = Quaternion.Euler(0f, 0f, shipAngle);
            if (Mathf.Abs(AimDirection.x) > 0.1f)
            {
                shipFacingLeft = AimDirection.x < 0f;
            }
            shipSprite.flipY = shipFacingLeft;
        }
    }

    // Resuelve el ángulo de la nave: giro libre mientras dura el parry, asentamiento suave hacia
    // el aim real al terminar/cancelarse, o el aim real directo el resto del tiempo.
    private float ResolveShipAngle(float aimAngle)
    {
        if (shipSpinning)
        {
            shipSpinAngle += parryData.spinSpeed * Time.deltaTime;
            return shipSpinAngle;
        }

        if (shipStabilizing)
        {
            stabilizeElapsed += Time.deltaTime;
            float t = parryData.stabilizeDuration > 0f
                ? Mathf.Clamp01(stabilizeElapsed / parryData.stabilizeDuration)
                : 1f;
            float eased = parryData.stabilizeCurve.Evaluate(t);
            if (t >= 1f) shipStabilizing = false;
            return Mathf.LerpAngle(stabilizeFromAngle, aimAngle, eased);
        }

        return aimAngle;
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
