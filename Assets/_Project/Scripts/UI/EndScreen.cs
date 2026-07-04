using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Pantalla ÚNICA de fin de partida (Victory / Death / Survived). Pura VISTA, igual que los binders del
// HUD: se suscribe a MatchController.MatchEnded y, según el resultado, escribe título y score. Muestra
// un prompt "PRESS ANY BUTTON" y, tras una breve gracia, CUALQUIER tecla/botón vuelve al menú (ideal
// para el cabinet, sin ratón ni navegación). Único acoplamiento: GameManager.LoadMenu().
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
    [SerializeField] private TMP_Text subtitle;    // opcional (puede no existir en la pantalla)
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("Pantalla de nickname (mismo CanvasPopups). Si el score califica en el top 100, el 'pulsa cualquier botón' va ahí en vez de al menú.")]
    [SerializeField] private NicknameEntryScreen nicknameScreen;

    [Header("Continuar")]
    [Tooltip("Gracia (segundos, sin escalar) antes de aceptar 'pulsa cualquier botón', para no saltar la pantalla por accidente.")]
    [SerializeField] private float inputDelay = 0.5f;

    [Header("Presentación por resultado")]
    [SerializeField] private OutcomeStyle[] styles =
    {
        new OutcomeStyle { outcome = MatchController.MatchOutcome.Victory,  title = "VICTORY",      subtitle = "Has derrotado al jefe", color = new Color(1f, 0.85f, 0.30f) },
        new OutcomeStyle { outcome = MatchController.MatchOutcome.Death,    title = "GAME OVER",    subtitle = "Tu nave ha caído",      color = new Color(0.90f, 0.25f, 0.27f) },
        new OutcomeStyle { outcome = MatchController.MatchOutcome.Survived, title = "YOU SURVIVED", subtitle = "Se acabó el tiempo",    color = new Color(0.40f, 0.80f, 1f) },
    };

    [Tooltip("Formato del score mostrado. {0} = puntuación.")]
    [SerializeField] private string scoreFormat = "SCORE  {0}";

    [Header("Retro (hoja halftone + tipografía)")]
    [Tooltip("Fuente retro (Archivo Black) aplicada a Título/Score/Prompt al arrancar. Vacío = fuente actual.")]
    [SerializeField] private TMP_FontAsset retroFont;
    [Tooltip("Texto 'PRESS ANY BUTTON' (para tipografía y tinta por hoja). Si está vacío se busca el hijo 'Prompt'.")]
    [SerializeField] private TMP_Text promptText;
    [Tooltip("Material HalftoneNoise por desenlace. Vacío = sin hoja para ese desenlace (queda el Dim).")]
    [SerializeField] private Material sheetVictory;
    [SerializeField] private Material sheetDeath;
    [SerializeField] private Material sheetSurvived;
    [Tooltip("Hoja halftone animada de fondo (se crea en runtime). Desmarcar para la pantalla clásica.")]
    [SerializeField] private bool sheetFx = true;

    private EndScreenSheetFx fx;

    // Estado de "esperando pulsación": activo mientras la pantalla está visible tras la gracia.
    private bool waitingForInput;
    private float shownAtUnscaled;
    private MatchController.MatchResult lastResult;   // para decidir el destino al continuar

    private void Awake()
    {
        // El panel del placeholder se oculta por CanvasGroup (alpha 0); si no se cableó, resuélvelo.
        if (panelGroup == null && panelRoot != null) panelGroup = panelRoot.GetComponent<CanvasGroup>();
        SetVisible(false);   // oculto hasta el final de la partida

        if (promptText == null && panelRoot != null)
        {
            Transform p = panelRoot.transform.Find("Prompt");
            if (p != null) promptText = p.GetComponent<TMP_Text>();
        }

        // Tipografía retro unificada (tmp.font = asset elige solo el material por defecto de la fuente).
        if (retroFont != null)
        {
            if (title != null) title.font = retroFont;
            if (scoreText != null) scoreText.font = retroFont;
            if (promptText != null) promptText.font = retroFont;
        }

        if (sheetFx && panelRoot != null)
            fx = EndScreenSheetFx.Create((RectTransform)panelRoot.transform);
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
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadMenu();   // resetea timeScale y enruta (flujo normal, arrancando desde el menú)
        }
        else
        {
            // Fallback: arrancando Play directamente en la escena Game no existe el GameManager
            // (se crea en Menu). Cargamos el menú a mano y restauramos el timeScale que EndMatch dejó a 0.
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameManager.MenuScene);
        }
    }

    private void Show(MatchController.MatchResult result)
    {
        lastResult = result;
        OutcomeStyle style = ResolveStyle(result.Outcome);

        if (title != null) { title.text = style.title; title.color = style.color; }
        if (subtitle != null) subtitle.text = style.subtitle;
        if (scoreText != null) scoreText.text = string.Format(scoreFormat, result.Score);

        // Hoja halftone del desenlace (las tres son oscuras: la tinta clara de la escena vale siempre).
        if (fx != null) fx.Apply(SheetFor(result.Outcome));

        SetVisible(true);

        // A partir de aquí, tras la gracia, cualquier botón vuelve al menú (ver Update).
        waitingForInput = true;
        shownAtUnscaled = Time.unscaledTime;
    }

    private void Update()
    {
        if (!waitingForInput) return;
        // Time.unscaledTime porque la partida está congelada (timeScale = 0).
        if (Time.unscaledTime - shownAtUnscaled < inputDelay) return;

        if (AnyButtonPressed())
        {
            waitingForInput = false;
            Continue();
        }
    }

    // Destino del "pulsa cualquier botón": si el score entra en el top 100 (cualquier desenlace,
    // estilo arcade), pasa a la pantalla de nickname; si no, al menú como siempre.
    private void Continue()
    {
        if (nicknameScreen != null && Leaderboard.Qualifies(lastResult.Score))
        {
            SetVisible(false);   // esta pantalla se retira; la de nickname toma el relevo
            nicknameScreen.Show(lastResult.Score);
        }
        else
        {
            GoToMenu();
        }
    }

    // "Pulsa cualquier botón": el encoder del cabinet emite teclas, así que basta con el teclado; se
    // añade el gamepad por comodidad al desarrollar. wasPressedThisFrame exige una pulsación NUEVA (no
    // cuenta un botón que ya venía mantenido al terminar la partida).
    private static bool AnyButtonPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;

        var pad = Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
                            pad.startButton.wasPressedThisFrame)) return true;

        return false;
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

    private Material SheetFor(MatchController.MatchOutcome outcome)
    {
        switch (outcome)
        {
            case MatchController.MatchOutcome.Victory: return sheetVictory;
            case MatchController.MatchOutcome.Death: return sheetDeath;
            case MatchController.MatchOutcome.Survived: return sheetSurvived;
            default: return null;
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
