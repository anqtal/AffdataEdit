using UnityEngine;
using Text = TMPro.TMP_Text;

namespace Arcade.Util.UnityExtension
{
	public static class TextExtension
	{
		public static float CalculateHeight(this Text text, string str)
		{
			return text.GetPreferredValues(str, text.rectTransform.rect.width, Mathf.Infinity).y;
		}
		public static float CalculateWidth(this Text text, string str)
		{
			return text.GetPreferredValues(str, Mathf.Infinity, Mathf.Infinity).x;
		}
	}
}
