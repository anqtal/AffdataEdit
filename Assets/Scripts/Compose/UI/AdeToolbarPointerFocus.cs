using UnityEngine;
using UnityEngine.EventSystems;

namespace Arcade.Compose
{
    // A mouse click should not leave a toolbar command in keyboard-selection state.
    public sealed class AdeToolbarPointerFocus : MonoBehaviour, IPointerUpHandler
    {
        public void OnPointerUp(PointerEventData eventData)
        {
            var events = EventSystem.current;
            if (eventData.button == PointerEventData.InputButton.Left && events
                && events.currentSelectedGameObject == gameObject)
                events.SetSelectedGameObject(null);
        }
    }
}
