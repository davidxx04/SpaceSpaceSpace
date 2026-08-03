using UnityEngine;

// Feedback de impacto por SILUETA: al recibir daño pinta el sprite de un color plano (blanco) un
// instante y restaura. Gemelo de DamageFlash, pero en vez de multiplicar SpriteRenderer.color (que
// no blanquea un sprite ya blanco / con material propio) delega en SpriteSolidFlash, que intercambia
// al shader AfterimageSolid y devuelve el material original al terminar. Ideal para enemigos con
// material glow, como el boss.
//
// Solo lee el evento Damaged del Health del mismo objeto; el combate no sabe que este VFX existe.
[RequireComponent(typeof(Health))]
public class HitSolidFlash : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private SpriteSolidFlash flash;
    [SerializeField] private Color flashColor = Color.white;
    [Tooltip("Instante que dura el flash blanco por golpe (segundos). Pequeñísimo por diseño.")]
    [SerializeField] private float flashDuration = 0.05f;

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        if (flash == null) flash = GetComponent<SpriteSolidFlash>();
        if (flash == null) flash = GetComponentInChildren<SpriteSolidFlash>();
    }

    private void OnEnable()
    {
        if (health != null) health.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }

    private void OnDamaged(DamageInfo _)
    {
        // SpriteSolidFlash ya es re-entrante (StopCoroutine + guard 'flashing'): golpes encadenados
        // mantienen el blanco y restauran una sola vez.
        if (flash != null) flash.Flash(flashColor, flashDuration);
    }
}
