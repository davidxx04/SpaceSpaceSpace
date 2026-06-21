using UnityEngine;

// Datos ajustables del efecto de estela (afterimage / "echo") de un dash, como asset
// (mismo patrón que RollData/AttackData). Tener varios assets (rol suave, dash intenso...)
// permite comparar sensaciones sin tocar código.
[CreateAssetMenu(menuName = "SpaceSpaceSpace/Afterimage Data", fileName = "AfterimageData")]
public class AfterimageData : ScriptableObject
{
    [Min(0.001f)]
    [Tooltip("Cada cuántos segundos se deja una estela. Menor = rastro más denso.")]
    public float spawnInterval = 0.04f;

    [Min(0.001f)]
    [Tooltip("Cuánto tarda cada estela en desvanecerse, en segundos.")]
    public float lifetime = 0.25f;

    [Tooltip("Color y opacidad de la estela a lo largo de su vida (0 = recién creada, 1 = a punto de morir).")]
    public Gradient colorOverLife = DefaultGradient();

    [Tooltip("Desplazamiento del orden de dibujo respecto a la nave (negativo = por detrás).")]
    public int sortingOffset = -1;

    [Tooltip("Material opcional para las estelas (p. ej. uno aditivo para un brillo blanco). Vacío = el de la nave.")]
    public Material material;

    [Tooltip("Usar tiempo sin escalar (sigue animando aunque haya pausa o cámara lenta).")]
    public bool useUnscaledTime = false;

    // Blanco semitransparente -> totalmente transparente.
    private static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        return g;
    }
}
