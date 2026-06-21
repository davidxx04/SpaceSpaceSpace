using UnityEngine;

// Una "estela" (ghost): copia congelada del sprite de la nave que se desvanece y se
// autodestruye. La crea AfterimageEmitter; no se usa ni se configura a mano. El color sale
// del AfterimageData y, con el shader AfterimageSolid, se ve como una silueta sólida.
[RequireComponent(typeof(SpriteRenderer))]
public class Afterimage : MonoBehaviour
{
    private SpriteRenderer sr;
    private Color color;
    private AnimationCurve alphaOverLife;
    private float lifetime;
    private float age;
    private bool useUnscaledTime;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    // Arranca el desvanecido. RGB = color elegido; alpha = color.a * alphaOverLife(edad 0..1).
    public void Begin(Sprite sprite, Color color, AnimationCurve alphaOverLife, float lifetime, bool useUnscaledTime)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        sr.sprite = sprite;
        this.color = color;
        this.alphaOverLife = alphaOverLife;
        this.lifetime = Mathf.Max(0.0001f, lifetime);
        this.useUnscaledTime = useUnscaledTime;
        age = 0f;
        Apply(0f);
    }

    private void Update()
    {
        age += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        Apply(t);

        if (age >= lifetime) Destroy(gameObject);
    }

    private void Apply(float t)
    {
        float fade = alphaOverLife != null ? alphaOverLife.Evaluate(t) : 1f - t;
        sr.color = new Color(color.r, color.g, color.b, color.a * fade);
    }
}
