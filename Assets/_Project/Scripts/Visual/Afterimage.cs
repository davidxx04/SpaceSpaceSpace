using UnityEngine;

// Una "estela" (ghost): copia congelada del sprite de la nave que se desvanece y se
// autodestruye. La crea AfterimageEmitter; no se usa ni se configura a mano.
[RequireComponent(typeof(SpriteRenderer))]
public class Afterimage : MonoBehaviour
{
    private SpriteRenderer sr;
    private Gradient colorOverLife;
    private float lifetime;
    private float age;
    private bool useUnscaledTime;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    // Arranca el desvanecido. El color/opacidad sale del gradiente según la edad 0..1.
    public void Begin(Sprite sprite, Gradient colorOverLife, float lifetime, bool useUnscaledTime)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        sr.sprite = sprite;
        this.colorOverLife = colorOverLife;
        this.lifetime = Mathf.Max(0.0001f, lifetime);
        this.useUnscaledTime = useUnscaledTime;
        age = 0f;
        sr.color = colorOverLife.Evaluate(0f);
    }

    private void Update()
    {
        age += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        sr.color = colorOverLife.Evaluate(t);

        if (age >= lifetime) Destroy(gameObject);
    }
}
