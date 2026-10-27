using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Arcade.Compose
{
    // TMP selects rows on pointer enter. Focus therefore cannot identify the hovered row.
    public sealed class AdeDropdownPointerFocus : MonoBehaviour
    {
        private Toggle item;
        private RectTransform rect, viewport;
        private Canvas canvas;
        private Camera eventCamera;
        private static int inputFrame = -1;
        private static bool pointerNavigation = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() { inputFrame = -1; pointerNavigation = true; }


        private void OnEnable()
        {
            item = GetComponent<Toggle>();
            rect = (RectTransform)transform;
            item.transition = Selectable.Transition.None;
            item.targetGraphic.CrossFadeColor(Color.white, 0, true, true);
        }

        private void OnTransformParentChanged()
        {
            canvas = null;
            viewport = null;
        }

        private void LateUpdate()
        {
            // TMP instantiates an item before assigning its popup parent.
            if (!canvas)
            {
                canvas = GetComponentInParent<Canvas>();
                if (!canvas) return;
                canvas = canvas.rootCanvas;
                eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var scroll = GetComponentInParent<ScrollRect>();
                viewport = scroll ? scroll.viewport : null;
            }
            var mouse = Mouse.current;
            if (inputFrame != Time.frameCount)
            {
                inputFrame = Time.frameCount;
                if (mouse != null && (mouse.delta.ReadValue() != Vector2.zero || mouse.scroll.ReadValue() != Vector2.zero
                    || mouse.leftButton.wasPressedThisFrame)) pointerNavigation = true;
                if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) pointerNavigation = false;
            }
            bool hovered = pointerNavigation && mouse != null
                && RectTransformUtility.RectangleContainsScreenPoint(rect, mouse.position.ReadValue(), eventCamera)
                && (!viewport || RectTransformUtility.RectangleContainsScreenPoint(viewport, mouse.position.ReadValue(), eventCamera));
            bool keyboardFocus = !pointerNavigation && EventSystem.current
                && EventSystem.current.currentSelectedGameObject == gameObject;
            item.targetGraphic.color = hovered || keyboardFocus ? AdeUiTheme.Current.Selection : Color.clear;
        }
    }
}
