using Arcade.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Compose
{
    public sealed partial class AdeUiTheme
    {
        private Material hudMaterial;

        private void StylePlaybackHud()
        {
            var compose = ArcadeComposeManager.Instance;
            if (!compose) return;
            // Reuse the original silhouettes, glyphs, and hit areas in both light and dark.
            // The material only maps their grayscale surfaces into the current palette.
            if (!hudMaterial) hudMaterial = new Material(Resources.Load<Shader>("AffdataEdit/Theme/HudPalette"));
            hudMaterial.SetColor("_SurfaceColor", Current.Chrome);
            hudMaterial.SetColor("_RaisedColor", Current.Panel);
            hudMaterial.SetColor("_BorderColor", Color.Lerp(Current.Border, Current.Text, .18f));
            hudMaterial.SetColor("_TextColor", Current.Text);
            var panel = ArcSkinManager.Instance.SongInfo;
            panel.sprite = darkSprites[11];
            panel.material = hudMaterial;
            panel.color = Color.white;
            compose.PauseButtonImage.material = hudMaterial;
            compose.PauseButtonImage.color = Color.white;
            compose.TimingSliderHandle.color = Current.Text;
            var timingSlider = compose.TimingSliderHandle.GetComponentInParent<Slider>();
            if (timingSlider && timingSlider.fillRect && timingSlider.fillRect.TryGetComponent<Image>(out var timingFill))
                timingFill.color = Current.Text;
            compose.SetPlaySprite(darkSprites[12]);
            compose.SetPlayPressedSprite(darkSprites[13]);
            compose.SetPauseSprite(darkSprites[14]);
            compose.SetPausePressedSprite(darkSprites[15]);
            AlignToolbarEdges(compose.LeftBarView, compose.PauseButtonImage.rectTransform, compose.BottomBarView);
            AlignToolbarEdges(compose.RightBarView, panel.rectTransform, compose.BottomBarView);
            var score = ArcScoreManager.Instance.ScoreText;
            score.color = Current.Text;
            score.fontSharedMaterial.SetColor("_OutlineColor", Current.Chrome);
            foreach (var label in panel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label == score) continue;
                label.color = label.transform.IsChildOf(panel.transform.Find("Cover")) ? Color.white
                    : label.name == "Placeholder" ? Current.Muted : Current.Text;
            }
        }

        private static void AlignToolbarEdges(RectTransform bar, RectTransform header, RectTransform footer)
        {
            var layout = bar.GetComponent<VerticalLayoutGroup>();
            var corners = new Vector3[4];
            header.GetWorldCorners(corners);
            float topEdge = bar.rect.yMax - bar.InverseTransformPoint(corners[0]).y;
            footer.GetWorldCorners(corners);
            float bottomEdge = bar.InverseTransformPoint(corners[1]).y - bar.rect.yMin;
            var buttons = bar.GetComponentsInChildren<Button>();
            // Tool backgrounds extend beyond their 60px hit rects. Align the full square.
            float topOutset = buttons.Length == 0 ? 0 : BackgroundOutset(buttons[0], true);
            float bottomOutset = buttons.Length == 0 ? 0 : BackgroundOutset(buttons[buttons.Length - 1], false);
            int top = Mathf.CeilToInt(topEdge + topOutset - .001f);
            int bottom = Mathf.CeilToInt(bottomEdge + bottomOutset - .001f);
            layout.padding = new RectOffset(layout.padding.left, layout.padding.right, top, bottom);
        }

        private static float BackgroundOutset(Button button, bool top)
        {
            var rect = (RectTransform)button.transform;
            var corners = new Vector3[4];
            button.targetGraphic.rectTransform.GetWorldCorners(corners);
            return Mathf.Max(0, top ? rect.InverseTransformPoint(corners[1]).y - rect.rect.yMax
                : rect.rect.yMin - rect.InverseTransformPoint(corners[0]).y);
        }
    }
}
