using System;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;

namespace Arcade.Compose.Dialog
{
	public class AdeBasicSingleDialogContent : AdeDialogContent<AdeSingleDialog>
	{
		public static AdeBasicSingleDialogContent Instance { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStaticState() => Instance = null;
		private void Awake()
		{
			Instance = this;
			Dialog.OnClose += OnClose;
		}
		private void OnDestroy()
		{
			Dialog.OnClose -= OnClose;
		}

		public Text Content;
		public Button CopyButton;
		public ScrollRect ContentScroll;

		private Action callback;

		public void Show(string Content, string Title = null, string Button1 = null, Action Callback = null, bool Copyable = false)
		{
			this.Content.text = Content;
			this.Dialog.Title.text = Title ?? "提示";
			this.Dialog.ButtonText.text = Button1 ?? "确认";
			callback = Callback;
			if (CopyButton) CopyButton.gameObject.SetActive(Copyable);
			OpenDialog();
			if (ContentScroll) ContentScroll.verticalNormalizedPosition = 1;
		}
		public void CopyContent() => GUIUtility.systemCopyBuffer = Content.text;
		public void OnClose()
		{
			callback?.Invoke();
			callback = null;
		}
	}
}
