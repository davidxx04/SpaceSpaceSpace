using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pantalla ÚNICA de fin de partida (Victory / Death / Survived). Pura VISTA, igual que los binders del
// HUD: se suscribe a MatchController.MatchEnded y, según el resultado, escribe título/subtítulo/score y
// deja el botón "Volver al menú" PRESELECCIONADO para el cabinet (cualquier botón = Submit lo confirma,
// sin ratón). No conoce combate, input ni físicas; el único acoplamiento es GameManager.LoadMenu().
//
// Vive en un objeto SIEMPRE ACTIVO (la raíz de CanvasPopups) y enciende el panel al terminar; así su
// OnEnable siempre llega a suscribirse aunque el panel arranque oculto.
public class EndScreen : MonoBehaviour
{
    // Texto/colores por desenlace, editables desde el Inspector (data-driven, sin tocar código).
    [Serializable]
    private struct OutcomeStyle
    {
        public MatchController.MatchOutcome outcome;
        public string title;
        [TextArea] public string subtitle;
        public Color color;
    }

    [Header("Refs")]
    [SerializeField] private MatchController match;
    [SerializeField] private GameObject panelRoot;    // panel a mostrar/ocultar (oculto en Play hasta el final)
    [SerializeField] private CanvasGroup panelGroup;  // opcional: si el panel se oculta por alpha (auto-resuelto)
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Button menuButton;

    [Header("Presentación por resultado")]
    [SerializeField] private OutcomeStyle[] styles =
    {
        new OutcomeStyle { outcome = MatchController.MatchOutcome.Victory,  title = "VICTORY",      subtitle = "Has derrotado al jefe", color = new Color(1f, 0.85f, 0.30f) },
        new OutcomeStyle { outcome = MatchController.MatchOutcome.Death,    title = "GAME OVER",    subtitle = "Tu nave ha caído",      color = new Color(0.90f, 0.25f, 0.27f) },
        new OutcomeStyle { outcome = MatchController.MatchOutcome.Survived, title = "YOU SURVIVED", subtitle = "Se acabó el tiempo",    color = new Color(0.40f, 0.80f, 1f) },
    };

    [Tooltip("Formato del score mostrado. {0} = puntuación.")]
    [SerializeField] private string scoreFormat = "SCORE  {0}";

    private void Awake()
    {
        // Un único botón (de momento): siempre vuelve al menú. Se cablea por código porque el
        // GameManager es DontDestroyOnLoad y no es referenciable desde el Inspector de la escena Game.
        if (menuButton != null)
            menuButton.onClick.AddListener(GoToMenu);

        // El panel del placeholder se oculta por CanvasGroup (alpha 0); si no se cableó, resuélvelo.
        if (panelGroup == null && panelRoot != null) panelGroup = panelRoot.GetComponent<CanvasGroup>();
        SetVisible(false);   // oculto hasta el final de la partida
    }

    private void OnEnable()
    {
        if (match != null) match.MatchEnded += Show;
    }

    private void OnDisable()
    {
        if (match != null) match.MatchEnded -= Show;
    }

    private void Start()
    {
        // Defensa por orden de ejecución: si la partida ya terminó antes de suscribirnos, mostrarla.
        if (match != null && match.IsOver) Show(match.Result);
    }

    private void GoToMenu()
    {
        if (GameManager.Instance != null) GameManager.Instance.LoadMenu();
    }

    private void Show(MatchController.MatchResult result)
    {
        OutcomeStyle style = ResolveStyle(result.Outcome);

        if (title != null) { title.text = style.title; title.color = style.color; }
        if (subtitle != null) subtitle.text = style.subtitle;
        if (scoreText != null) scoreText.text = string.Format(scoreFormat, result.Score);

        SetVisible(true);

        // Cabinet: deja la opción marcada para que cualquier botón (Submit) la confirme sin ratón.
        if (menuButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(menuButton.gameObject);
    }

    // Muestra/oculta el panel. Prefiere CanvasGroup (permite fade y no desactiva los hijos); si no
    // hay, cae a SetActive del GameObject.
    private void SetVisible(bool visible)
    {
        if (panelGroup != null)
        {
            if (panelRoot != null && !panelRoot.activeSelf) panelRoot.SetActive(true);
            panelGroup.alpha = visible ? 1f : 0f;
            panelGroup.interactable = visible;
            panelGroup.blocksRaycasts = visible;
        }
        else if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }
    }

    private OutcomeStyle ResolveStyle(MatchController.MatchOutcome outcome)
    {
        for (int i = 0; i < styles.Length; i++)
            if (styles[i].outcome == outcome) return styles[i];

        // Fallback si falta una entrada en el Inspector (no debería pasar).
        return new OutcomeStyle { outcome = outcome, title = outcome.ToString().ToUpper(), subtitle = string.Empty, color = Color.white };
    }
}
