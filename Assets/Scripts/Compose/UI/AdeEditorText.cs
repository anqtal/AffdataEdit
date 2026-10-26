using TMPro;
using UnityEngine;

namespace Arcade.Compose
{
    internal static class AdeEditorText
    {
        public const float BodySize = 24, HeadingSize = 30, CaptionSize = 20;
        private static TMP_FontAsset font;
        public static TMP_FontAsset Font => font ? font : (font = Resources.Load<TMP_FontAsset>("AffdataEdit/Fonts/EditorUI SDF"));
        public static void Configure(TMP_Text text, float size)
        {
            text.font = Font;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.richText = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
        }
    }
}
