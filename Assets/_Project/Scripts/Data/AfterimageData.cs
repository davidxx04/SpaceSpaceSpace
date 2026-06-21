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

    [Tooltip("Color de la estela. Se dibuja como una silueta SÓLIDA de este color (no los colores de la nave).")]
    public Color color = Color.white;

    [Tooltip("Opacidad a lo largo de la vida (1 = recién creada, 0 = desaparecida). Se multiplica por el alpha del color.")]
    public AnimationCurve alphaOverLife = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Tooltip("Desplazamiento del orden de dibujo respecto a la nave (negativo = por detrás).")]
    public int sortingOffset = -1;

    [Tooltip("Material opcional. Vacío = silueta sólida (shader AfterimageSolid, creado por código). " +
             "Asigna uno aditivo si quieres un brillo más intenso.")]
    public Material material;

    [Tooltip("Usar tiempo sin escalar (sigue animando aunque haya pausa o cámara lenta).")]
    public bool useUnscaledTime = false;
}
