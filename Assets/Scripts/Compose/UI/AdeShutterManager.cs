using Arcade.Audio;
using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace Arcade.Compose
{
	public class AdeShutterManager : MonoBehaviour
	{
		public const float OpenDuration = 0.3f;
		public const float CloseDuration = 0.75f;
		public static AdeShutterManager Instance { get; private set; }
		private void Awake()
		{
			if (Instance != null)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			DontDestroyOnLoad(gameObject);
		}

		public RectTransform Left, Right;
		[System.NonSerialized] public BassClip CloseAudio, OpenAudio;

		private static float OpenEase(float time, float duration, float amplitude, float period)
		{
			return Mathf.Pow(time / duration, 1.5f);
		}

		public void Open()
		{
			Left.DOKill();
			Right.DOKill();
			Left.DOPivotX(1, OpenDuration).SetEase(OpenEase);
			Right.DOPivotX(0, OpenDuration).SetEase(OpenEase);
			BassAudio.PlayOneShot(OpenAudio, true);
		}
		public void Close()
		{
			Left.DOKill();
			Right.DOKill();
			Left.DOPivotX(0, CloseDuration).SetEase(Ease.OutCubic);
			Right.DOPivotX(1, CloseDuration).SetEase(Ease.OutCubic);
			BassAudio.PlayOneShot(CloseAudio, true);
		}
		public IEnumerator OpenCoroutine()
		{
			Open();
			yield return new WaitForSeconds(OpenDuration);
		}
		public IEnumerator CloseCoroutine()
		{
			Close();
			yield return new WaitForSeconds(CloseDuration);
		}
	}
}
