using UnityEngine;

// Aura del parry: un "escudo de energía" (shader SpaceSpaceSpace/EnergyShield) visible solo durante
// la ventana activa; empieza grande y encoge (curva) hasta desaparecer al cerrarse, de modo que el
// jugador entiende el tiempo efectivo del parry de un vistazo. El shader dibuja la forma (pentágono
// con punta abajo) con borde brillante + interior translúcido + halo; aquí solo se tinta (color de
// vértice), se escala y se da un DESTELLO al abrirse la ventana. Va en un HIJO de la raíz Player
// (centrado en la nave, sin heredar la rotación/recoil del Visual). Solo lee API pública del
// controller y eventos del receptor de daño, así que no acopla nada.
//
// Además se DISIPA en cuanto el parry se resuelve (acierto -> ParrySuccess, o golpe -> Hit) sin
// esperar a que la ventana termine, para que el feedback case con lo que acaba de pasar.
[RequireComponent(typeof(SpriteRenderer))]
public class ParryAura : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerDamageReceiver receiver;
    [SerializeField] private Color color = new Color(0.3f, 0.6f, 1f, 1f);   // azul
    [SerializeField] private float startScale = 1.5f;

    [Tooltip("Escala del anillo a lo largo de la ventana (0 = recién abierta -> 1; 1 = a punto de cerrar -> 0).")]
    [SerializeField] private AnimationCurve scaleOverWindow = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("Destello de activación")]
    [Tooltip("Cuánto se amplifica el brillo del escudo en el instante de abrirse la ventana (0 = sin destello).")]
    [SerializeField] private float flashBoost = 2.5f;

    [Tooltip("Forma del destello a lo largo de la ventana (X = progreso 0..1, Y = intensidad). Brillo al abrir -> se apaga.")]
    [SerializeField] private AnimationCurve flashOverWindow =
        new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.3f, 0f));

    private SpriteRenderer sr;
    private bool resolved;   // el parry actual ya se resolvió (acierto/golpe): ocultar hasta cerrar la ventana

    // Material del escudo compartido (código de color por SpriteRenderer.color). Cacheado static y
    // creado por Shader.Find, mismo patrón que SpriteGlow: sin arte ni cableado de material.
    private static Material shieldMaterial;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.sprite = PrimitiveQuad.Unit;              // el shader dibuja la forma dentro del quad
        sr.sharedMaterial = GetShieldMaterial();     // ignora el material del Inspector
        if (player == null) player = GetComponentInParent<PlayerController>();
        if (receiver == null) receiver = GetComponentInParent<PlayerDamageReceiver>();
        sr.enabled = false;
    }

    private static Material GetShieldMaterial()
    {
        if (shieldMaterial == null)
        {
            Shader shader = Shader.Find("SpaceSpaceSpace/EnergyShield");
            if (shader != null) shieldMaterial = new Material(shader) { name = "EnergyShield (auto)" };
        }
        return shieldMaterial;
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

            // Destello al abrirse la ventana: amplifica el brillo del escudo (additive) y decae.
            float flash = 1f + flashBoost * Mathf.Max(0f, flashOverWindow.Evaluate(player.ParryWindowProgress));
            Color c = color;
            c.a *= flash;
            sr.color = c;

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
