using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;

namespace Arcade.Compose
{

	public class AdeHotkeyRebindingButton : MonoBehaviour
	{
		public string HotkeyName;
		public Text Text;

		public void StartRebinding()
		{
			AdeInputManager.Instance.SetHotkeyRebindingButton(this);
		}
	}
}