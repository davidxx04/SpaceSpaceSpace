using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Popup de la tabla de puntuaciones en la escena Menu. Mismo patrón de mostrar/ocultar que
// EndScreen (panel siempre presente, oculto por CanvasGroup) y misma filosofía de cabina que el
// resto de la UI (palanca = teclas, sin ratón).
//
// La lista es de SOLO LECTURA: no se navega fila a fila con el EventSystem (100 selectables sería
// pésima UX); se mueve un ScrollRect leyendo el eje Y de la acción UI/Navigate. Mientras el popup
// está abierto se desactiva la interacción del menú de fondo para que la palanca no navegue los
// botones por detrás, y al cerrar se devuelve el foco al botón que lo abrió.
public class LeaderboardPopup : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("CanvasGroup del panel del popup. Se muestra/oculta por alpha (arranca oculto).")]
    [SerializeField] private CanvasGroup panelGroup;

    [Header("Lista")]
    [SerializeField] private ScrollRect scrollRect;
    [Tooltip("Contenedor donde se instancian las filas. Si se deja vacío, se usa scrollRect.content.")]
    [SerializeField] private RectTransform content;
    [SerializeField] private LeaderboardRow rowPrefab;
    [Tooltip("Objeto que se muestra cuando la tabla está vacía (texto tipo 'Aún no hay puntuaciones').")]
    [SerializeField] private GameObject emptyState;

    [Header("Menú de fondo")]
    [Tooltip("CanvasGroup del menú principal: se desactiva su interacción mientras el popup está abierto.")]
    [SerializeField] private CanvasGroup menuGroup;
    [Tooltip("Botón a re-seleccionar al cerrar (normalmente Btn_Leaderboard), para devolver el foco.")]
    [SerializeField] private Selectable returnSelection;

    [Header("Scroll")]
    [Tooltip("Velocidad de scroll en fracción de lista por segundo (se multiplica por el eje Y de la palanca).")]
    [SerializeField] private float scrollSpeed = 0.6f;

    [Header("Cierre")]
    [Tooltip("Gracia (segundos) antes de aceptar el cierre, para que la misma pulsación que abre no lo cierre.")]
    [SerializeField] private float inputDelay = 0.3f;

    private ArcadeControls controls;
    private readonly List<LeaderboardRow> spawnedRows = new List<LeaderboardRow>();
    private bool isOpen;
    private float shownAtUnscaled;

    private void Awake()
    {
        if (content == null && scrollRect != null) content = scrollRect.content;

        controls = new ArcadeControls();
        controls.UI.Cancel.performed += OnClosePressed;
        controls.UI.Submit.performed += OnClosePressed;

        SetVisible(false);   // oculto hasta que se pulse el botón Leaderboard
    }

    private void OnDestroy()
    {
        if (controls != null)
        {
            controls.UI.Cancel.performed -= OnClosePressed;
            controls.UI.Submit.performed -= OnClosePressed;
            controls.Dispose();
        }
    }

    // Cableado a Btn_Leaderboard.OnClick desde el Inspector.
    public void Show()
    {
        BuildRows();
        SetVisible(true);

        // Bloquea la navegación del menú de fondo mientras el popup manda.
        if (menuGroup != null) menuGroup.interactable = false;

        controls.UI.Enable();
        isOpen = true;
        shownAtUnscaled = Time.unscaledTime;
    }

    public void Hide()
    {
        SetVisible(false);
        controls.UI.Disable();
        isOpen = false;

        // La MISMA pulsación (J/K/L) que cierra sigue "viva" este frame para el EventSystem: si se
        // reactivara el menú y se re-seleccionara el botón ya mismo, su Submit procesaría esa pulsación
        // y reabriría el popup en el acto. El control se devuelve un frame después.
        StartCoroutine(RestoreMenuNextFrame());
    }

    private System.Collections.IEnumerator RestoreMenuNextFrame()
    {
        yield return null;   // deja pasar el frame de la pulsación que cerró

        if (menuGroup != null) menuGroup.interactable = true;
        var es = EventSystem.current;
        if (es != null && returnSelection != null && returnSelection.IsInteractable())
            es.SetSelectedGameObject(returnSelection.gameObject);
    }

    private void Update()
    {
        if (!isOpen || scrollRect == null) return;

        // Palanca arriba/abajo = mover la lista. verticalNormalizedPosition: 1 = arriba, 0 = abajo;
        // W (eje Y +1) debe subir, así que se suma. Time.unscaledDeltaTime por robustez.
        float navY = controls.UI.Navigate.ReadValue<Vector2>().y;
        if (!Mathf.Approximately(navY, 0f))
        {
            float pos = scrollRect.verticalNormalizedPosition + navY * scrollSpeed * Time.unscaledDeltaTime;
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(pos);
        }
    }

    private void OnClosePressed(InputAction.CallbackContext _)
    {
        if (!isOpen) return;
        if (Time.unscaledTime - shownAtUnscaled < inputDelay) return;   // respeta la gracia
        Hide();
    }

    // Reconstruye las filas desde el estado actual de la tabla (puede cambiar entre aperturas).
    private void BuildRows()
    {
        // Limpia las filas anteriores.
        for (int i = 0; i < spawnedRows.Count; i++)
            if (spawnedRows[i] != null) Destroy(spawnedRows[i].gameObject);
        spawnedRows.Clear();

        IReadOnlyList<LeaderboardEntry> entries = Leaderboard.GetAll();

        if (emptyState != null) emptyState.SetActive(entries.Count == 0);

        if (rowPrefab != null && content != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                LeaderboardRow row = Instantiate(rowPrefab);
                row.transform.SetParent(content, false);
                row.Set(i + 1, entries[i].nickname, entries[i].score);
                spawnedRows.Add(row);
            }
        }

        // Empieza arriba del todo. Fuerza el layout para que el ScrollRect ya tenga tamaños válidos.
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    // Igual que EndScreen: preferimos CanvasGroup (permite fade y no desactiva hijos).
    private void SetVisible(bool visible)
    {
        if (panelGroup == null) return;
        if (!panelGroup.gameObject.activeSelf) panelGroup.gameObject.SetActive(true);
        panelGroup.alpha = visible ? 1f : 0f;
        panelGroup.interactable = visible;
        panelGroup.blocksRaycasts = visible;
    }
}
