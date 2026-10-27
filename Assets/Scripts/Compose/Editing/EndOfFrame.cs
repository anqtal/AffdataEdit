using UnityEngine;
using UnityEngine.Events;

namespace Arcade.Compose
{
	public class EndOfFrame : MonoBehaviour
	{
		public static EndOfFrame Instance { get; private set; }

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStaticState() => Instance = null;
		private void Awake()
		{
			Instance = this;
		}

		[HideInInspector]
		public UnityEvent Listeners = new UnityEvent();

		private void Update()
		{
			Listeners.Invoke();
			Listeners.RemoveAllListeners();
		}
	}
}
