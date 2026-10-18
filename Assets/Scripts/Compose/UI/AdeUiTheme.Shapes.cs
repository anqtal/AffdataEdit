using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arcade.Compose
{
	public sealed partial class AdeUiTheme
	{
		private const float PanelRadius = 16, ControlRadius = 8;
		private readonly Dictionary<string, Sprite> shapes = new Dictionary<string, Sprite>();
		private readonly Dictionary<string, Sprite> originalControlSprites = new Dictionary<string, Sprite>();
		private const string ControlShapePrefix = "ThemeControl/";

		// One geometry for the fill and its 1px inset border. Slicing preserves the radius
		// at any panel size; 2x sampling keeps the edge smooth on scaled canvases.
		private Sprite Shape(string name, float radius, Color fill, Color border, bool topOnly = false)
		{
			string key = (Dark ? "Dark/" : "Light/") + name;
			if (shapes.TryGetValue(key, out Sprite sprite)) return sprite;
			const int scale = 2;
			int size = Mathf.CeilToInt((radius * 2 + 4) * scale);
			float half = size / (2f * scale);
			var pixels = new Color32[size * size];
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float r = topOnly && y < size / 2 ? 0 : radius;
					Vector2 q = new Vector2(Mathf.Abs((x + 0.5f) / scale - half), Mathf.Abs((y + 0.5f) / scale - half)) - Vector2.one * (half - r);
					float distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
					Color color = Color.Lerp(border, fill, Mathf.Clamp01(0.5f - (distance + 1) * scale));
					color.a *= Mathf.Clamp01(0.5f - distance * scale);
					pixels[y * size + x] = color;
				}
			}
			var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = key, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			texture.SetPixels32(pixels);
			texture.Apply(false, true);
			float slice = (radius + 1) * scale;
			sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100 * scale, 0, SpriteMeshType.FullRect,
				new Vector4(slice, topOnly ? scale : slice, slice, slice));
			sprite.name = name;
			shapes.Add(key, sprite);
			return sprite;
		}

		private void CreateDialogShapes()
		{
			darkSprites[0] = Shape("ThemeHeader", PanelRadius, C(0x181818), C(0x3C3C3C), true);
			darkSprites[1] = Shape("ThemePanel", PanelRadius, C(0x1F1F1F), C(0x3C3C3C));
			for (int i = 2; i <= 10; i += 3)
			{
				bool primary = i != 5;
				darkSprites[i] = Shape(primary ? "ThemePrimary" : "ThemeSecondary", ControlRadius, C(primary ? 0x007ACCu : 0x2B2B2Bu), C(primary ? 0x168BD2u : 0x454545u));
				darkSprites[i + 1] = Shape(primary ? "ThemePrimaryDisabled" : "ThemeSecondaryDisabled", ControlRadius, C(0x303030), C(0x3C3C3C));
				darkSprites[i + 2] = Shape(primary ? "ThemePrimaryPressed" : "ThemeSecondaryPressed", ControlRadius, C(primary ? 0x0068ADu : 0x242424u), C(primary ? 0x168BD2u : 0x454545u));
			}
		}

		private void StyleControl(Selectable selectable)
		{
			if (selectable is Scrollbar || !(selectable.targetGraphic is Image image) || !image.sprite) return;
			string name = image.sprite.name;
			bool themed = name.StartsWith(ControlShapePrefix);
			if (themed) name = name.Substring(ControlShapePrefix.Length);
			if (name != "UISprite" && name != "InputFieldBackground") return;
			if (!themed) originalControlSprites[name] = image.sprite;
			if (Dark)
			{
				image.sprite = Shape(ControlShapePrefix + name, ControlRadius, C(0x313131), C(0x454545));
				image.type = UnityEngine.UI.Image.Type.Sliced;
				image.color = Color.white;
				var colors = ColorBlock.defaultColorBlock;
				colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
				colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
				colors.selectedColor = new Color(1.08f, 1.08f, 1.08f);
				colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
				selectable.colors = colors;
			}
			else if (themed && originalControlSprites.TryGetValue(name, out Sprite original))
			{
				image.sprite = original;
				image.color = Color.white;
				selectable.colors = ColorBlock.defaultColorBlock;
			}
		}

		private void OnDestroy()
		{
			foreach (Sprite sprite in shapes.Values)
			{
				Destroy(sprite.texture);
				Destroy(sprite);
			}
			if (Instance == this) Instance = null;
		}
	}
}
