using UnityEngine;
using UnityEngine.EventSystems;

// Sonidos de un botón: hover al SELECCIONARLO (palanca/EventSystem) o pasar el ratón, y click al
// SUBMIT (J/K/L) o clic de ratón. Se pone SOLO en los botones que deben sonar (menú); las teclas del
// nickname no lo llevan, así su navegación/pulsación queda muda (sería desagradable con tantas teclas).
// No guarda refs: llama a la API estática de AudioManager, que lee la mezcla del SfxLibrary.
[DisallowMultipleComponent]
public class ButtonSfx : MonoBehaviour,
    ISelectHandler, IPointerEnterHandler, ISubmitHandler, IPointerClickHandler
{
    public void OnSelect(BaseEventData _) => AudioManager.PlayUiHover();
    public void OnPointerEnter(PointerEventData _) => AudioManager.PlayUiHover();

    public void OnSubmit(BaseEventData _) => AudioManager.PlayUiClick();
    public void OnPointerClick(PointerEventData _) => AudioManager.PlayUiClick();
}
