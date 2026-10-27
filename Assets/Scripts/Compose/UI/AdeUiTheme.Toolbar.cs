using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Compose
{
    public sealed partial class AdeUiTheme
    {
        private static readonly Dictionary<Image, bool> toolbarToggleStates = new Dictionary<Image, bool>();
        private readonly List<Image> staleToolbarIcons = new List<Image>();
        internal static Color ToolbarActiveColor => new Color(0f, .47f, .83f, .4f);

        public static void SetToolbarToggleState(Image icon, bool active)
        {
            if (!icon) return;
            toolbarToggleStates[icon] = active;
            if (Instance) Instance.StyleToolbarToggle(icon, active);
        }

        private void ApplyToolbarToggleStates()
        {
            staleToolbarIcons.Clear();
            foreach (var state in toolbarToggleStates)
            {
                if (!state.Key) staleToolbarIcons.Add(state.Key);
                else StyleToolbarToggle(state.Key, state.Value);
            }
            foreach (var icon in staleToolbarIcons) toolbarToggleStates.Remove(icon);
        }

        private void StyleToolbarToggle(Image icon, bool active)
        {
            var button = icon.GetComponentInParent<Button>();
            if (!button || !button.image) return;
            if (!button.GetComponent<AdeToolbarPointerFocus>())
                button.gameObject.AddComponent<AdeToolbarPointerFocus>();

            // Keep the original square tool sprite and the same translucent fill as AUTO.
            button.image.color = active ? ToolbarActiveColor : Color.white;
            button.transition = Selectable.Transition.ColorTint;
            if (active) button.colors = ColorBlock.defaultColorBlock;
            else SetStates(button, ToolbarEntry);
            Color foreground = active ? Color.Lerp(Current.Text, Dark ? Color.white : Color.black, .25f) : Current.Text;
            icon.color = button.IsInteractable() ? foreground : Current.Muted;
        }
    }
}
