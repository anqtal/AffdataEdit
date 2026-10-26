using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Arcade.Compose
{
	public class AdeGameplayContentInputHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		public static AdeGameplayContentInputHandler Instance { get; private set; }

		private bool active = false;

		public static bool InputActive => Instance != null && Instance.active && !AdeMarkingMenuManager.BlocksEditorInput;
		private void Awake()
		{
			Instance = this;
		}
		public void OnPointerEnter(PointerEventData data)
		{
			active = true;
		}
		public void OnPointerExit(PointerEventData data)
		{
			active = false;
		}
	}
}
