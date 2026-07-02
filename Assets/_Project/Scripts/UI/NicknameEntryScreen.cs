using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pantalla de introducción de nickname cuando el score de la run entra en el top 100. Pura VISTA,
// mismo patrón que EndScreen: vive en la RAÍZ de CanvasPopups (siempre activa) con el panel oculto
// por CanvasGroup, y EndScreen la enciende con Show(score) tras el "press any button".
//
// El teclado en pantalla lo monta el builder (NicknameEntryBuilder): cada tecla es un Button de uGUI
// cableado a AppendChar/DeleteChar/Confirm, y la palanca lo navega con el EventSystem ya configurado
// (UI/Navigate = WASD, UI/Submit = J/K/L). Aquí no se lee input directamente: solo estado del nombre.
public class NicknameEntryScreen : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("CanvasGroup del panel. Se muestra/oculta por alpha (arranca oculto en Play).")]
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private TMP_Text rankText;
    [Tooltip("Campo donde se pinta el nombre según se teclea.")]
    [SerializeField] private TMP_Text nameText;

    [Header("Textos")]
    [Tooltip("Formato del mensaje de rango. {0} = posición alcanzada.")]
    [SerializeField] private string rankFormat = "YOU REACHED TOP {0}!";

    [Header("Nickname")]
    [SerializeField] private int maxLength = 10;

    [Tooltip("Gracia (segundos, sin escalar) antes de aceptar teclas: la pulsación que cerró el EndScreen no debe escribir.")]
    [SerializeField] private float inputDelay = 0.3f;

    private int pendingScore;
    private string nickname = string.Empty;
    private bool done;   // ya se confirmó (evita doble-insert mientras carga el menú)

    private void Awake()
    {
        SetVisible(false);   // oculto hasta que EndScreen decida que el score califica
    }

    // La llama EndScreen tras el "press any button" si Leaderboard.Qualifies(score).
    public void Show(int score)
    {
        pendingScore = score;
        nickname = string.Empty;
        done = false;

        int rank = Leaderboard.PeekRank(score);
        if (rank < 0) { GoToMenu(); return; }   // defensa: EndScreen ya comprobó Qualifies

        if (rankText != null) rankText.text = string.Format(rankFormat, rank);
        RefreshName();

        SetVisible(true);

        // Gracia anti-carrera (misma lección que LeaderboardPopup): la pulsación que cerró el
        // EndScreen sigue viva este frame; con el panel no-interactable el EventSystem no puede
        // pulsar ninguna tecla (y el UISelectionKeeper del panel tampoco selecciona todavía).
        if (panelGroup != null)
        {
            panelGroup.interactable = false;
            StartCoroutine(EnableAfterDelay());
        }
    }

    private IEnumerator EnableAfterDelay()
    {
        // Tiempo sin escalar: la partida está congelada (timeScale = 0).
        yield return new WaitForSecondsRealtime(inputDelay);
        if (panelGroup != null) panelGroup.interactable = true;
    }

    // --- Cableados a los onClick de las teclas por el builder ---

    public void AppendChar(string c)
    {
        if (done || string.IsNullOrEmpty(c) || nickname.Length >= maxLength) return;
        nickname += c;
        RefreshName();
    }

    public void DeleteChar()
    {
        if (done || nickname.Length == 0) return;
        nickname = nickname.Substring(0, nickname.Length - 1);
        RefreshName();
    }

    public void Confirm()
    {
        if (done || nickname.Length == 0) return;   // exige al menos 1 carácter
        done = true;

        Leaderboard.Insert(nickname, pendingScore);
        GoToMenu();
    }

    // ---

    // Caret "_" mientras quede hueco, para que se vea dónde se escribe.
    private void RefreshName()
    {
        if (nameText != null)
            nameText.text = nickname.Length < maxLength ? nickname + "_" : nickname;
    }

    // Igual que EndScreen.GoToMenu: flujo normal por GameManager; fallback para Play directo en Game.
    private void GoToMenu()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadMenu();
        }
        else
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(GameManager.MenuScene);
        }
    }

    // Mismo patrón que EndScreen: CanvasGroup para poder ocultar sin desactivar hijos.
    private void SetVisible(bool visible)
    {
        if (panelGroup == null) return;
        if (!panelGroup.gameObject.activeSelf) panelGroup.gameObject.SetActive(true);
        panelGroup.alpha = visible ? 1f : 0f;
        panelGroup.interactable = visible;
        panelGroup.blocksRaycasts = visible;
    }
}
