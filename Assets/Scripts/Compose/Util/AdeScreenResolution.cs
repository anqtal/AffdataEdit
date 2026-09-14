using System.Globalization;
using UnityEngine;

namespace Arcade.Compose
{
	public static class AdeScreenResolution
	{
		public static Vector2Int GetWindowedSize(string resolution, RectInt workArea)
		{
			string[] dimensions = resolution?.Split('x');
			if (dimensions == null || dimensions.Length != 2 ||
				!int.TryParse(dimensions[0], NumberStyles.None, CultureInfo.InvariantCulture, out int width) ||
				!int.TryParse(dimensions[1], NumberStyles.None, CultureInfo.InvariantCulture, out int height) ||
				width <= 0 || height <= 0)
			{
				width = 1280;
				height = 720;
			}

			// DPI is physical pixel density, not the operating system's window scale.
			// Leave room for window borders and the title bar, and preserve aspect ratio.
			if (workArea.width > 0 && workArea.height > 0)
			{
				int availableWidth = Mathf.Max(1, workArea.width - 32);
				int availableHeight = Mathf.Max(1, workArea.height - 64);
				float scale = Mathf.Min(1f, (float)availableWidth / width, (float)availableHeight / height);
				width = Mathf.Max(1, Mathf.FloorToInt(width * scale));
				height = Mathf.Max(1, Mathf.FloorToInt(height * scale));
			}

			return new Vector2Int(width, height);
		}
	}
}
