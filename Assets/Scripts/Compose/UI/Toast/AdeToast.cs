using System;
using UnityEngine;
using Text = TMPro.TMP_Text;
using LitMotion;

namespace Arcade.Compose
{
	public class AdeToast : MonoBehaviour
	{
		public const float Duration = 0.12f;
		public const float Stay = 1f;
		public static AdeToast Instance { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStaticState() => Instance = null;
		public RectTransform ContentRect;
		public Text Content;
		[SerializeField] private CanvasGroup noticeGroup;
		[SerializeField] private CanvasGroup[] toolbarControls = Array.Empty<CanvasGroup>();
		private MotionHandle toastMotion;
		private float toolbarAlpha = 1f;

		private void Awake()
		{
			Instance = this;
		}
		private void OnDisable()
		{
			toastMotion.TryCancel();
			RestoreToolbar();
		}
		private void OnDestroy()
		{
			toastMotion.TryCancel();
			if (Instance == this) Instance = null;
		}
		public void Show(string text)
		{
			if (!isActiveAndEnabled || string.IsNullOrWhiteSpace(text)) return;
			toastMotion.TryCancel();
			SetToolbarInteractable(false);
			// Replace a visible message only after it has faded out.
			if (noticeGroup.alpha > 0f) FadeNotice(0f, () => BeginNotice(text));
			else BeginNotice(text);
		}
		private void BeginNotice(string text)
		{
			float stay = Stay * Mathf.Max(1f, Content.GetPreferredValues(text, ContentRect.rect.width, 0f).y / 60f);
			Content.text = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
			Action showNotice = () => FadeNotice(1f, () => FadeNotice(0f, FadeToolbarIn, stay));
			if (toolbarAlpha <= 0f)
			{
				showNotice();
				return;
			}
			toastMotion = LMotion.Create(toolbarAlpha, 0f, Duration)
				.WithEase(Ease.OutQuad).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
				.WithOnComplete(showNotice)
				.Bind(SetToolbarAlpha);
		}
		private void FadeNotice(float alpha, Action onComplete, float delay = 0f)
		{
			toastMotion = LMotion.Create(noticeGroup.alpha, alpha, Duration)
				.WithEase(Ease.OutQuad).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
				.WithDelay(delay).WithOnComplete(onComplete)
				.Bind(value => noticeGroup.alpha = value);
		}
		private void FadeToolbarIn()
		{
			toastMotion = LMotion.Create(toolbarAlpha, 1f, Duration)
				.WithEase(Ease.OutQuad).WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
				.WithOnComplete(RestoreToolbar).Bind(SetToolbarAlpha);
		}
		private void RestoreToolbar()
		{
			if (noticeGroup != null) noticeGroup.alpha = 0f;
			SetToolbarAlpha(1f);
			SetToolbarInteractable(true);
			AdeUiTheme.Refresh();
		}
		private void SetToolbarAlpha(float alpha)
		{
			toolbarAlpha = alpha;
			foreach (var control in toolbarControls)
			{
				if (control != null) control.alpha = alpha;
			}
		}
		private void SetToolbarInteractable(bool interactable)
		{
			foreach (var control in toolbarControls)
			{
				if (control == null) continue;
				control.interactable = interactable;
				control.blocksRaycasts = interactable;
			}
		}
	}
}
