using System.Collections;
using UnityEngine;

// Feedback simple e instantáneo: parpadea el sprite al recibir daño. Se engancha al
// evento Damaged del Health del mismo objeto. Sirve para ver los golpes en el
// muñeco de pruebas (y luego en el boss).
[RequireComponent(typeof(Health))]
public class DamageFlash : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.08f;

    private Health health;
    private Color originalColor;
    private Coroutine routine;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) originalColor = spriteRenderer.color;
    }

    private void OnEnable() => health.Damaged += OnDamaged;
    private void OnDisable() => health.Damaged -= OnDamaged;

    private void OnDamaged(DamageInfo info)
    {
        if (spriteRenderer == null) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        spriteRenderer.color = originalColor;
    }
}
