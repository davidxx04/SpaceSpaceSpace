using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Mantiene SIEMPRE un elemento de UI seleccionado para que el joystick/mando de la recreativa tenga
// foco y se vea el highlight (sin ratón no hay "hover" que valga). Selecciona 'firstSelected' al
// aparecer y re-selecciona si el foco se pierde (p. ej. el ratón deselecciona al salir de un botón).
//
// Es la pieza que faltaba para navegar con palanca: el nuevo Input System mueve el foco con Navigate,
// pero solo si YA hay algo seleccionado; esto garantiza que siempre lo haya. Reutilizable en cualquier
// pantalla con botones (menú, leaderboard, etc.).
[DisallowMultipleComponent]
public class UISelectionKeeper : MonoBehaviour
{
    [Tooltip("Elemento seleccionado por defecto (normalmente el primer botón del menú).")]
    [SerializeField] private Selectable firstSelected;

    private void OnEnable() => SelectDefault();
    private void Start() => SelectDefault();   // por si el EventSystem no estaba listo en OnEnable

    private void Update()
    {
        var es = EventSystem.current;
        if (es == null) return;

        // Si nada está seleccionado (o lo seleccionado se desactivó), recupera el foco por defecto.
        GameObject cur = es.currentSelectedGameObject;
        if (cur == null || !cur.activeInHierarchy)
            SelectDefault();
    }

    private void SelectDefault()
    {
        var es = EventSystem.current;
        if (es == null || firstSelected == null) return;
        if (!firstSelected.gameObject.activeInHierarchy || !firstSelected.IsInteractable()) return;
        es.SetSelectedGameObject(firstSelected.gameObject);
    }
}
