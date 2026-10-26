using TMPro;
using UnityEngine;

namespace Arcade.Gameplay
{
    // Score and combo keep their existing bindings, using AffdataPlay's baked glyphs.
    public sealed class ArcBitmapText : MonoBehaviour
    {
        private TMP_Text label;
        private Material material;
        private bool score;

        public static void Attach(TMP_Text text, bool score)
        {
            var renderer = text.gameObject.AddComponent<ArcBitmapText>();
            renderer.label = text;
            renderer.score = score;
            var font = Resources.Load<TMP_FontAsset>("AffdataEdit/Fonts/Gameplay/" + (score ? "ScoreBitmap" : "ComboBitmap"));
            text.font = font;
            renderer.material = new Material(font.material);
            var outline = score ? (Color)new Color32(80, 60, 90, 255) : text.color;
            outline.a = 1;
            renderer.material.SetColor("_OutlineColor", outline);
            text.fontSharedMaterial = renderer.material;
            text.alignment = score ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false; text.raycastTarget = false; text.extraPadding = false;
            text.UpdateMeshPadding();
        }

        private void LateUpdate()
        {
            if (!score)
            {
                var outline = label.color;
                outline.a = 1;
                material.SetColor("_OutlineColor", outline);
            }
        }
        private void OnDestroy()
        {
            if (material) Destroy(material);
        }
    }
}
