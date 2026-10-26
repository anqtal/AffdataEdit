using TMPro;
using UnityEngine;

namespace Arcade.Compose
{
    // Private-use characters keep inline icons compatible with plain-text UI labels.
    internal static class AdeUiIcons
    {
        public const string Add = "\uE000", Minus = "\uE001", Close = "\uE002", Select = "\uE003";
        public const string Expand = "\uE004", Collapse = "\uE005", Info = "\uE006", Check = "\uE007";
        private static TMP_SpriteAsset symbols;

        public static void SetText(TMP_Text label, string text)
        {
            if (!symbols) symbols = Resources.Load<TMP_SpriteAsset>("AffdataEdit/Icons/TablerSymbols");
            label.spriteAsset = symbols;
            label.tintAllSprites = true;
            label.text = text;
        }
    }
}
