using System;
using System.Collections.Generic;
using LitMotion;
using UnityEngine;

namespace Arcade.Compose
{

	public abstract class AdeDialogContent<TDialog> : MonoBehaviour where TDialog : AdeDialog
	{
		public TDialog Dialog;
		public void OpenDialog()
		{
			Dialog.Open();
		}
		public void CloseDialog()
		{
			Dialog.Close();
		}
		public void SwitchDialogOpenState()
		{
			Dialog.SwitchOpenState();
		}
	}

	public abstract class AdeDialog : MonoBehaviour
	{
		public GameObject View;

		public Action OnOpen;
		public Action OnClose;
		public void Open()
		{
			AdeDialogManager.Instance.Open(this);
		}
		public void Close()
		{
			AdeDialogManager.Instance.Close(this);
		}
		public void SwitchOpenState()
		{
			AdeDialogManager.Instance.SwitchOpenState(this);
		}
	}

	public class AdeDialogManager : MonoBehaviour
	{
		public static AdeDialogManager Instance { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStaticState() => Instance = null;

		public Transform Opening, Closed;

		private const float TransitionDuration = 0.25f, ClosedScale = 1.08f;
		private readonly Dictionary<AdeDialog, MotionHandle> motions = new Dictionary<AdeDialog, MotionHandle>();
		private readonly HashSet<AdeDialog> closing = new HashSet<AdeDialog>();

		private void Awake()
		{
			Instance = this;
		}

		public void Open(AdeDialog dialog)
		{
			bool wasClosing = closing.Contains(dialog);
			dialog.OnOpen?.Invoke();
			dialog.transform.SetParent(Opening);
			// Opening an open dialog keeps it as it is instead of replaying the fade.
			if (dialog.View.activeSelf && !wasClosing) return;
			StopMotion(dialog);
			CanvasGroup group = Group(dialog);
			Transform view = dialog.View.transform;
			// A dialog reopened while closing continues from its current pose.
			float from = wasClosing ? group.alpha : 0;
			if (!wasClosing)
			{
				dialog.View.SetActive(true);
				Pose(group, view, 0);
			}
			group.blocksRaycasts = true;
			AdeUiTheme.OnDialogOpened();
			motions[dialog] = AnimateView(group, view, from, 1f, () => motions.Remove(dialog));
		}

		public void Close(AdeDialog dialog)
		{
			if (closing.Contains(dialog)) return;
			if (!dialog.View.activeSelf)
			{
				StopMotion(dialog);
				Hide(dialog);
				return;
			}
			CanvasGroup group = Group(dialog);
			Transform view = dialog.View.transform;
			// A dialog closed while opening fades out from its current pose.
			float from = group.alpha;
			StopMotion(dialog);
			// The dialog stops taking input at once; it is deactivated when the fade ends.
			closing.Add(dialog);
			group.blocksRaycasts = false;
			motions[dialog] = AnimateView(group, view, from, 0f, () =>
				{
					motions.Remove(dialog);
					closing.Remove(dialog);
					Hide(dialog);
				});
		}

		private void Hide(AdeDialog dialog)
		{
			Pose(Group(dialog), dialog.View.transform, 1);
			dialog.View.SetActive(false);
			dialog.transform.SetParent(Closed);
			dialog.OnClose?.Invoke();
		}

		private void StopMotion(AdeDialog dialog)
		{
			if (motions.TryGetValue(dialog, out MotionHandle handle)) handle.TryCancel();
			motions.Remove(dialog);
			closing.Remove(dialog);
		}

		private static CanvasGroup Group(AdeDialog dialog)
		{
			var group = dialog.View.GetComponent<CanvasGroup>();
			return group ? group : dialog.View.AddComponent<CanvasGroup>();
		}

		internal static MotionHandle AnimateView(CanvasGroup group, Transform view, float from, float to, Action completed)
		{
			return LMotion.Create(from, to, TransitionDuration).WithEase(Ease.OutSine)
				.WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
				.WithOnComplete(completed).Bind(t => Pose(group, view, t));
		}

		internal static void Pose(CanvasGroup group, Transform view, float t)
		{
			if (!group) return;
			group.alpha = t;
			view.localScale = Vector3.one * Mathf.LerpUnclamped(ClosedScale, 1, t);
		}

		public void SwitchOpenState(AdeDialog dialog)
		{
			if (dialog.View.activeSelf && !closing.Contains(dialog))
			{
				Close(dialog);
			}
			else
			{
				Open(dialog);
			}
		}
	}
}
