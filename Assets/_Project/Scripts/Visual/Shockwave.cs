using UnityEngine;

// Onda expansiva genérica: un sprite (anillo) que crece de startScale a endScale y se desvanece
// a lo largo de 'duration', y luego se autodestruye. Reutilizable para cualquier impacto
// (parry acertado, golpe del boss, explosión...). Se usa como prefab que se instancia.
[RequireComponent(typeof(SpriteRenderer))]
public class Shockwave : MonoBehaviour
{
    [SerializeField] private float startScale = 0.2f;
    [SerializeField] private float endScale = 2f;
    [Min(0.0001f)]
    [SerializeField] private float duration = 0.3f;
    [Tooltip("Progreso del escalado a lo largo de la vida (0..1).")]
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Opacidad a lo largo de la vida (1 = inicio, 0 = desaparecida).")]
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private Color color = Color.white;
    [SerializeField] private bool useUnscaledTime = false;

    private SpriteRenderer sr;
    private float age;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    private void OnEnable()
    {
        age = 0f;
        Apply(0f);
    }

    private void Update()
    {
        age += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        Apply(t);

        if (age >= duration) Destroy(gameObject);
    }

    private void Apply(float t)
    {
        float s = Mathf.LerpUnclamped(startScale, endScale, scaleCurve.Evaluate(t));
        transform.localScale = Vector3.one * s;

        Color c = color;
        c.a = color.a * alphaCurve.Evaluate(t);
        sr.color = c;
    }
}
