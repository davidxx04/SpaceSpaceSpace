using System.Collections;
using UnityEngine;

// Oculta el HUD in-game (barras + timer) cuando termina la partida. VISTA pura, mismo patrón que
// EndScreen: vive en el propio canvas del HUD (CanvasHUDInGame), se suscribe a MatchController.MatchEnded
// y baja el CanvasGroup a alpha 0. El combate/gameplay siguen sin saber nada del HUD.
//
// Como MatchEnded solo dispara UNA vez y a partir de ahí el flujo es pantalla final -> nickname ->
// menú (que recarga la escena con un HUD nuevo), ocultar una sola vez cubre las tres pantallas
// (Victory / Survived / Death) y la de introducir el nombre: el HUD no vuelve dentro de la misma run.
[RequireComponent(typeof(CanvasGroup))]
public class HudVisibility : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("MatchController de la escena. Si se deja vacío se resuelve solo en Awake.")]
    [SerializeField] private MatchController match;

    [Header("Ocultado")]
    [Tooltip("Duración del fundido al terminar la partida (tiempo SIN escalar: la run está congelada). 0 = instantáneo.")]
    [SerializeField] private float fadeDuration = 0.25f;

    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();

        // Fallback: si no se cableó desde el Inspector/builder, búscalo en la escena.
        if (match == null) match = FindObjectOfType<MatchController>();
    }

    private void OnEnable()
    {
        if (match != null) match.MatchEnded += OnMatchEnded;
    }

    private void OnDisable()
    {
        if (match != null) match.MatchEnded -= OnMatchEnded;
    }

    private void Start()
    {
        // Defensa por orden de ejecución: si la partida ya terminó antes de suscribirnos, oculta ya.
        if (match != null && match.IsOver) HideNow();
    }

    private void OnMatchEnded(MatchController.MatchResult _)
    {
        if (fadeDuration > 0f && isActiveAndEnabled) StartCoroutine(FadeOut());
        else HideNow();
    }

    private IEnumerator FadeOut()
    {
        float start = group.alpha;
        float t = 0f;
        // La partida está congelada (timeScale = 0): tiempo sin escalar.
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, 0f, t / fadeDuration);
            yield return null;
        }
        HideNow();
    }

    private void HideNow()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
