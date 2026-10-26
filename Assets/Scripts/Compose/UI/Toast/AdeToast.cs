using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TMP_Text;
using Arcade.Util.UnityExtension;
using LitMotion;
using LitMotion.Extensions;

namespace Arcade.Compose
{
	public class AdeToast : MonoBehaviour
	{
		public const float Duration = 0.4f;
		public const float Stay = 1f;
		public static AdeToast Instance { get; private set; }
		public RectTransform ToastRect;
		public RectTransform ContentRect;
		public Text Content;
		private MotionHandle toastMotion;

		private void Awake()
		{
			Instance = this;
		}
		private void OnDestroy()
		{
			toastMotion.TryCancel();
		}
		public void Show(string text)
		{
			toastMotion.TryComplete();
			// Completing the entrance can schedule an exit; replace it with this toast's exit.
			toastMotion.TryCancel();
			float height = Content.CalculateHeight(text);
			Content.text = text;
			toastMotion = LMotion.Create(ToastRect.pivot.y, 1f, Duration)
				.WithEase(Ease.OutQuad).WithScheduler(MotionScheduler.Update)
				.WithOnComplete(() =>
				{
					toastMotion = LMotion.Create(ToastRect.pivot.y, 0f, Duration)
						.WithEase(Ease.OutQuad).WithScheduler(MotionScheduler.Update)
						.WithDelay(Stay * height / 60)
						.BindToPivotY(ToastRect);
				})
				.BindToPivotY(ToastRect);
		}
	}
}
