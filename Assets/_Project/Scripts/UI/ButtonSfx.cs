using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Sonidos de un botón: hover al SELECCIONARLO (palanca/EventSystem) o pasar el ratón, y click al
// SUBMIT (J/K/L) o clic de ratón. Se pone SOLO en los botones que deben sonar (menú); las teclas del
// nickname no lo llevan, así su navegación/pulsación queda muda (sería desagradable con tantas teclas).
// No guarda refs de audio: llama a la API estática de AudioManager, que lee la mezcla del SfxLibrary.
//
// Respeta la INTERACTUABILIDAD del botón: si no se puede pulsar, no suena. Selectable.IsInteractable()
// tiene en cuenta los CanvasGroup padres, así que cuando un popup bloquea el menú de fondo (p. ej.
// LeaderboardPopup pone menuGroup.interactable = false) estos botones se quedan mudos solos, sin que
// este componente sepa nada de qué popup hay abierto.
[DisallowMultipleComponent]
public class ButtonSfx : MonoBehaviour,
    ISelectHandler, IPointerEnterHandler, ISubmitHandler, IPointerClickHandler
{
    private Selectable selectable;

    private void Awake() => selectable = GetComponent<Selectable>();

    // Sin Selectable (caso raro) se reproduce igual: no rompe nada.
    private bool CanPlay => selectable == null || selectable.IsInteractable();

    public void OnSelect(BaseEventData _) { if (CanPlay) AudioManager.PlayUiHover(); }
    public void OnPointerEnter(PointerEventData _) { if (CanPlay) AudioManager.PlayUiHover(); }

    public void OnSubmit(BaseEventData _) { if (CanPlay) AudioManager.PlayUiClick(); }
    public void OnPointerClick(PointerEventData _) { if (CanPlay) AudioManager.PlayUiClick(); }
}
