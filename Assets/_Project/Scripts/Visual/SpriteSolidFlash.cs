using System.Collections;
using UnityEngine;

// Flash de silueta sólida reutilizable: durante un instante pinta el sprite de un color plano
// (usando el shader AfterimageSolid) y luego restaura su material y color originales. Sirve para
// "pops" de impacto (parry acertado, golpe del boss...). No sabe nada del juego: quien quiera lo
// dispara con Flash(color, duración).
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteSolidFlash : MonoBehaviour
{
    private SpriteRenderer sr;
    private Material originalMaterial;
    private Color originalColor;
    private Coroutine routine;
    private bool flashing;

    // Material de silueta sólida, creado una sola vez por código a partir del shader y compartido.
    private static Material solidMaterial;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    private static Material GetSolidMaterial()
    {
        if (solidMaterial == null)
        {
            Shader shader = Shader.Find("SpaceSpaceSpace/AfterimageSolid");
            if (shader != null) solidMaterial = new Material(shader) { name = "AfterimageSolid (auto)" };
        }
        return solidMaterial;
    }

    // Pinta la silueta del color dado durante 'duration' segundos y restaura.
    public void Flash(Color color, float duration)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        // Captura los originales SOLO si no estábamos ya en mitad de un flash (para no guardar
        // el material de silueta como "original" y dejar la nave en blanco).
        if (!flashing)
        {
            originalMaterial = sr.sharedMaterial;
            originalColor = sr.color;
            flashing = true;
        }

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(FlashRoutine(color, duration));
    }

    private IEnumerator FlashRoutine(Color color, float duration)
    {
        Material solid = GetSolidMaterial();
        if (solid != null) sr.sharedMaterial = solid;
        sr.color = color;

        yield return new WaitForSeconds(duration);

        sr.sharedMaterial = originalMaterial;
        sr.color = originalColor;
        flashing = false;
        routine = null;
    }
}
