using System.Collections;
using System.Collections.Generic;
using Arcade.Compose.Editing;
using Arcade.Gameplay;
using Arcade.Gameplay.Chart;
using UnityEngine.InputSystem;

namespace Arcade.Compose.Operation
{
	public class AdeSelectNoteOperation : AdeOperation
	{
		public static AdeSelectNoteOperation Instance { get; private set; }

		private void Awake()
		{
			Instance = this;
		}

		private float? rangeSelectPosition = null;
		public float? RangeSelectPosition { get => rangeSelectPosition; }

		public override AdeOperationResult TryExecuteOperation()
		{
			if (!AdeInputManager.Instance.Inputs.RangeSelection.IsPressed())
			{
				rangeSelectPosition = null;
			}

			if (!AdeGameplayContentInputHandler.InputActive)
			{
				return false;
			}

			if (Mouse.current.leftButton.wasPressedThisFrame)
			{
				//range selection shortcut
				if (AdeInputManager.Instance.Inputs.RangeSelection.IsPressed())
				{
					if (rangeSelectPosition == null)
					{
						rangeSelectPosition = AdeCursorManager.Instance.AttachedTiming;
					}
					else
					{
						if (!AdeInputManager.Instance.Inputs.MultipleSelection.IsPressed())
						{
							AdeSelectionManager.Instance.DeselectAllNotes();
						}
						AdeSelectionManager.Instance.RangeSelectNote(rangeSelectPosition.Value, AdeCursorManager.Instance.AttachedTiming);
						rangeSelectPosition = null;
						return true;
					}
				}

				ArcNote n = ArcNoteRenderer.PickNote(AdeCursorManager.Instance.GameplayCamera, Mouse.current.position.ReadValue());
				if (n != null)
				{
					if (!AdeInputManager.Instance.Inputs.MultipleSelection.IsPressed())
					{
						AdeSelectionManager.Instance.DeselectAllNotes();
						AdeSelectionManager.Instance.SelectNote(n);
						return true;
					}
					else
					{
						if (AdeSelectionManager.Instance.SelectedNotes.Contains(n)) AdeSelectionManager.Instance.DeselectNote(n);
						else AdeSelectionManager.Instance.SelectNote(n);
						return true;
					}
				}
				else
				{
					if (!AdeInputManager.Instance.Inputs.MultipleSelection.IsPressed() && !AdeClickToCreate.Instance.MayAddArcTapOnArc())
					{
						AdeSelectionManager.Instance.DeselectAllNotes();
					}
				}
			}
			return false;
		}
		public override void Reset()
		{
			rangeSelectPosition = null;
		}
	}
}
