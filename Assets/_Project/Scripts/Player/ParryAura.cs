using UnityEngine;

// Anillo de "timing" del parry: visible solo durante la ventana activa; empieza grande y encoge
// (curva) hasta desaparecer al cerrarse, de modo que el jugador entiende el tiempo efectivo del
// parry de un vistazo. Va en un HIJO de la raíz Player (centrado en la nave, sin heredar la
// rotación/recoil del Visual). Solo lee API pública del controller y eventos del receptor de
// daño, así que no acopla nada.
//
// Además se DISIPA en cuanto el parry se resuelve (acierto -> ParrySuccess, o golpe -> Hit) sin
// esperar a que la ventana termine, para que el feedback case con lo que acaba de pasar.
[RequireComponent(typeof(SpriteRenderer))]
public class ParryAura : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerDamageReceiver receiver;
    [SerializeField] private Color color = Color.yellow;
    [SerializeField] private float startScale = 1.5f;

    [Tooltip("Escala del anillo a lo largo de la ventana (0 = recién abierta -> 1; 1 = a punto de cerrar -> 0).")]
    [SerializeField] private AnimationCurve scaleOverWindow = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    private SpriteRenderer sr;
    private bool resolved;   // el parry actual ya se resolvió (acierto/golpe): ocultar hasta cerrar la ventana

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (player == null) player = GetComponentInParent<PlayerController>();
        if (receiver == null) receiver = GetComponentInParent<PlayerDamageReceiver>();
        sr.enabled = false;
    }

    private void OnEnable()
    {
        if (receiver != null)
        {
            receiver.ParrySuccess += OnResolved;
            receiver.Hit += OnResolved;
        }
    }

    private void OnDisable()
    {
        if (receiver != null)
        {
            receiver.ParrySuccess -= OnResolved;
            receiver.Hit -= OnResolved;
        }
    }

    // El anillo se disipa en cuanto el parry se resuelve, sin esperar a que cierre la ventana.
    private void OnResolved(DamageInfo _) => resolved = true;

    private void LateUpdate()
    {
        if (player == null) return;

        if (player.IsParrying && !resolved)
        {
            sr.enabled = true;
            sr.color = color;
            float s = startScale * scaleOverWindow.Evaluate(player.ParryWindowProgress);
            transform.localScale = Vector3.one * s;
        }
        else
        {
            sr.enabled = false;
        }

        // Rearmar para la siguiente ventana en cuanto esta se cierra.
        if (!player.IsParrying) resolved = false;
    }
}
