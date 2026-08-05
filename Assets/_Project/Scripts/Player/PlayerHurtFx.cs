using UnityEngine;

// Feedback de "dañado" del jugador: al recibir un golpe REAL (ni parry ni i-frames del rol),
// muestra una animación de daño. Escucha PlayerDamageReceiver.Hit y reactiva un overlay que lleva
// un SpriteFlipbook configurado como one-shot ida-y-vuelta (pingPong, sin loop, disableObjectOnFinish):
// el flipbook se auto-oculta al terminar y vuelve a arrancar limpio en cada golpe.
//
// Solo lee el evento público del receptor de daño; el combate no sabe que este VFX existe.
public class PlayerHurtFx : MonoBehaviour
{
    [SerializeField] private PlayerDamageReceiver receiver;
    [SerializeField] private PlayerController player;

    [Tooltip("Overlay con SpriteRenderer + SpriteFlipbook (pingPong, sin loop, disableObjectOnFinish). Empieza desactivado.")]
    [SerializeField] private GameObject hurtOverlay;

    [Tooltip("Parpadeo de la nave durante los i-frames de cortesía. Opcional.")]
    [SerializeField] private SpriteBlink invulnerabilityBlink;

    private SpriteRenderer overlaySr;
    private bool blinking;   // parpadeando por un golpe reciente (dura mientras siga invulnerable)

    private void Awake()
    {
        if (receiver == null) receiver = GetComponentInParent<PlayerDamageReceiver>();
        if (player == null) player = GetComponentInParent<PlayerController>();
        if (hurtOverlay != null)
        {
            overlaySr = hurtOverlay.GetComponent<SpriteRenderer>();
            hurtOverlay.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        // Igual que el propulsor: el overlay es hijo del Visual y hereda la rotación, pero flipY es
        // del SpriteRenderer y NO se hereda; hay que copiarlo para que las direcciones izquierda casen.
        if (overlaySr != null && player != null && hurtOverlay.activeSelf)
            overlaySr.flipY = player.ShipFacingLeft;

        // Parpadeo de i-frames: arranca con el golpe y dura exactamente lo que el jugador siga
        // invulnerable (un roll normal no dispara Hit, así que no parpadea).
        if (invulnerabilityBlink != null)
        {
            if (blinking && (player == null || !player.IsInvulnerable)) blinking = false;
            invulnerabilityBlink.Active = blinking;
        }
    }

    private void OnEnable()
    {
        if (receiver != null) receiver.Hit += OnHit;
    }

    private void OnDisable()
    {
        if (receiver != null) receiver.Hit -= OnHit;
    }

    private void OnHit(DamageInfo info)
    {
        // El receptor ya concedió la invulnerabilidad antes de avisar: arrancamos el parpadeo.
        blinking = true;

        AudioManager.PlayPlayerHurt();

        if (hurtOverlay != null)
        {
            // Reactivar fuerza OnEnable -> Play() y reinicia la animación aunque ya estuviese en curso
            // (golpes encadenados).
            hurtOverlay.SetActive(false);
            hurtOverlay.SetActive(true);
        }
    }
}
