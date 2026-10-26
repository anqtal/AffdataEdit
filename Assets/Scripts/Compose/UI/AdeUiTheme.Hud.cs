using Arcade.Gameplay;
using TMPro;
using UnityEngine;

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
            compose.SetPlaySprite(darkSprites[12]);
            compose.SetPlayPressedSprite(darkSprites[13]);
            compose.SetPauseSprite(darkSprites[14]);
            compose.SetPausePressedSprite(darkSprites[15]);
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
    }
}
