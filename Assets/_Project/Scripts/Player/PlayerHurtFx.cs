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

    private SpriteRenderer overlaySr;

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

    // Igual que el propulsor: el overlay es hijo del Visual y hereda la rotación, pero flipY es del
    // SpriteRenderer y NO se hereda; hay que copiarlo para que las direcciones a la izquierda casen.
    private void LateUpdate()
    {
        if (overlaySr != null && player != null && hurtOverlay.activeSelf)
            overlaySr.flipY = player.ShipFacingLeft;
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
        if (hurtOverlay == null) return;

        // Reactivar fuerza OnEnable -> Play() y reinicia la animación aunque ya estuviese en curso
        // (golpes encadenados).
        hurtOverlay.SetActive(false);
        hurtOverlay.SetActive(true);
    }
}
