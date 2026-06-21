using UnityEngine;

// Propulsor de la nave: pegamento específico del jugador que decide QUÉ animación se muestra
// (moviéndose -> flight, rodando -> turbo, quieto -> apagado) y sincroniza el flipY con la nave.
// Va en un HIJO del Visual, así hereda rotación y posición gratis; solo el flipY necesita
// copiarse porque es propiedad del SpriteRenderer y no se hereda a los hijos.
// La animación en sí la reproduce un SpriteFlipbook genérico; este componente solo lo orquesta.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(SpriteFlipbook))]
public class ShipThruster : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerController player;

    [Header("Animaciones")]
    [Tooltip("Fotogramas del propulsor mientras la nave se mueve normal.")]
    [SerializeField] private Sprite[] flightFrames;

    [Tooltip("Fotogramas del propulsor durante el roll (turbo).")]
    [SerializeField] private Sprite[] turboFrames;

    [Tooltip("Velocidad (fps) de la animación de movimiento normal.")]
    [SerializeField] private float flightFps = 12f;

    [Tooltip("Velocidad (fps) de la animación de turbo (roll).")]
    [SerializeField] private float turboFps = 18f;

    [Header("Activación")]
    [Tooltip("Módulo de input por encima del cual se considera que la nave se mueve.")]
    [SerializeField] private float moveThreshold = 0.1f;

    private SpriteRenderer sr;
    private SpriteFlipbook flipbook;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        flipbook = GetComponent<SpriteFlipbook>();
        if (player == null) player = GetComponentInParent<PlayerController>();
    }

    // En LateUpdate: UpdateAim ya ha fijado el facing de la nave en este frame.
    private void LateUpdate()
    {
        // La rotación y la posición se heredan del Visual (padre). Solo replicamos el volteo.
        sr.flipY = player.ShipFacingLeft;

        if (player.IsRolling)
        {
            Show(turboFrames, turboFps);
        }
        else if (player.Input.MoveInput.sqrMagnitude > moveThreshold * moveThreshold)
        {
            Show(flightFrames, flightFps);
        }
        else
        {
            sr.enabled = false;   // quieto: propulsor apagado
        }
    }

    private void Show(Sprite[] frames, float fps)
    {
        sr.enabled = true;
        flipbook.FramesPerSecond = fps;
        flipbook.SetFrames(frames);
    }
}
