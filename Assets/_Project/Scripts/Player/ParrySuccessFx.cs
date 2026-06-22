using UnityEngine;

// Orquesta el feedback al ACERTAR un parry. Se suscribe a PlayerDamageReceiver.ParrySuccess y
// dispara los sub-efectos, todos OPCIONALES (null-check) para poder activar/desactivar piezas y
// probar combinaciones: flash de la nave, onda expansiva, camera shake y chispas. No sabe nada
// del combate; solo escucha el evento.
public class ParrySuccessFx : MonoBehaviour
{
    [Header("Refs (todas opcionales)")]
    [SerializeField] private PlayerDamageReceiver receiver;
    [SerializeField] private SpriteSolidFlash shipFlash;
    [SerializeField] private Shockwave shockwavePrefab;
    [SerializeField] private LightningBurst lightningPrefab;
    [SerializeField] private CameraShake cameraShake;
    [Tooltip("Prefab de chispas (p. ej. un ParticleSystem con Stop Action = Destroy). Opcional.")]
    [SerializeField] private GameObject sparksPrefab;

    [Header("Flash de la nave")]
    [SerializeField] private Color flashColor = new Color(1f, 1f, 0.8f, 1f);   // blanco-amarillento
    [SerializeField] private float flashDuration = 0.06f;

    [Header("Camera shake")]
    [SerializeField] private float shakeAmplitude = 0.15f;
    [SerializeField] private float shakeDuration = 0.12f;

    private void Awake()
    {
        if (receiver == null) receiver = GetComponent<PlayerDamageReceiver>();
        if (cameraShake == null && Camera.main != null) cameraShake = Camera.main.GetComponent<CameraShake>();
    }

    private void OnEnable()
    {
        if (receiver != null) receiver.ParrySuccess += OnParrySuccess;
    }

    private void OnDisable()
    {
        if (receiver != null) receiver.ParrySuccess -= OnParrySuccess;
    }

    private void OnParrySuccess(DamageInfo info)
    {
        Vector3 pos = transform.position;

        if (shipFlash != null) shipFlash.Flash(flashColor, flashDuration);
        if (shockwavePrefab != null) Instantiate(shockwavePrefab, pos, Quaternion.identity);
        if (lightningPrefab != null) Instantiate(lightningPrefab, pos, Quaternion.identity);
        if (sparksPrefab != null) Instantiate(sparksPrefab, pos, Quaternion.identity);
        if (cameraShake != null) cameraShake.Shake(shakeAmplitude, shakeDuration);
    }
}
