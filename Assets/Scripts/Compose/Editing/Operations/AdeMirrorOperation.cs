using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using Arcade.Gameplay.Chart;
using Arcade.Compose;
using Arcade.Compose.MarkingMenu;
using Arcade.Compose.Command;
using Arcade.Gameplay;

namespace Arcade.Compose.Operation
{
	public class AdeMirrorOperation : AdeOperation
	{
		public static AdeMirrorOperation Instance { get; private set; }

		public MarkingMenuItem Entry;
		private MarkingMenuItem verticalEntry;
		private InputAction verticalMirror;

		public override bool IsOnlyMarkingMenu => false;
		public override MarkingMenuItem[] MarkingMenuItems
		{
			get
			{
				if (!ArcGameplayManager.Instance.IsLoaded) return null;
				if (AdeCursorManager.Instance == null) return null;
				if (AdeSelectionManager.Instance.SelectedNotes.Count == 0) return null;
				return verticalEntry && AdeSelectionManager.Instance.SelectedNotes.Exists(note => note is ArcArc)
                    ? new[] { Entry, verticalEntry } : new[] { Entry };
			}
		}

		private void Awake()
		{
			Instance = this;
		}

        private void Start()
        {
            Entry.StartupText = "水平镜像";
            Entry.Text = Entry.StartupText;
            verticalEntry = Instantiate(Entry, Entry.transform.parent);
            verticalEntry.name = "Vertical mirror";
            verticalEntry.StartupText = "上下翻转";
            verticalEntry.HasSubMenu = false;
            verticalEntry.SubItems = new MarkingMenuItem[0];
            verticalEntry.OnHangOver = new UnityEvent();
            verticalEntry.OnConfirmed = new UnityEvent();
            verticalEntry.OnConfirmed.AddListener(ManuallyFlipVertically);
            verticalEntry.gameObject.SetActive(false);
            verticalMirror = AdeInputManager.Instance.Hotkeys.Get().FindAction("VerticalMirror", true);
        }

        private void FlipSelectedNotesVertically()
        {
            if (!ArcGameplayManager.Instance.IsLoaded) return;
            var commands = new List<ICommand>();
            // Alpha mirrors around the current sky range, including enwiden.
            float top = ArcAlgorithm.WorldYToArc(ArcSceneControlManager.Instance.SkyInput.localPosition.y);
            foreach (var note in AdeSelectionManager.Instance.SelectedNotes)
            {
                if (!(note is ArcArc arc)) continue;
                var flipped = (ArcArc)arc.Clone();
                flipped.YStart = top - arc.YStart;
                flipped.YEnd = top - arc.YEnd;
                commands.Add(new EditArcEventCommand(arc, flipped));
            }
            if (commands.Count > 0)
                AdeCommandManager.Instance.Add(new BatchCommand(commands.ToArray(), "上下翻转"));
        }

        public void ManuallyFlipVertically()
        {
            AdeOperationManager.Instance.TryExecuteOperation(() =>
            {
                FlipSelectedNotesVertically();
                return null;
            });
        }

        private void OnDestroy()
        {
            if (verticalEntry) Destroy(verticalEntry.gameObject);
        }

		private void MirrorSelectedNotes()
		{
			var selected = AdeSelectionManager.Instance.SelectedNotes;
			List<ICommand> commands = new List<ICommand>();
			foreach (var n in selected)
			{
				switch (n)
				{
					case ArcSlide slide:
						var mirrored = (ArcSlide)slide.Clone();
						mirrored.StartCenter = 1 - slide.StartCenter; mirrored.EndCenter = 1 - slide.EndCenter;
						mirrored.LeftCurve = slide.RightCurve; mirrored.RightCurve = slide.LeftCurve;
						commands.Add(new EditArcEventCommand(slide, mirrored));
						break;
					case ArcTap tap:
						ArcTap newtap = tap.Clone() as ArcTap;
						if (newtap.FloatLane.HasValue) newtap.FloatLane = 1 - newtap.FloatLane.Value;
                        else newtap.Track = 5 - newtap.Track;
						commands.Add(new EditArcEventCommand(tap, newtap));
						break;
					case ArcHold hold:
						ArcHold newhold = hold.Clone() as ArcHold;
						if (newhold.FloatLane.HasValue) newhold.FloatLane = 1 - newhold.FloatLane.Value;
                        else newhold.Track = 5 - newhold.Track;
						commands.Add(new EditArcEventCommand(hold, newhold));
						break;
					case ArcArc arc:
						ArcArc newarc = arc.Clone() as ArcArc;
						newarc.XStart = 1 - newarc.XStart;
						newarc.XEnd = 1 - newarc.XEnd;
						if (newarc.Color < 2)
						{
							newarc.Color = 1 - newarc.Color;
						}
						commands.Add(new EditArcEventCommand(arc, newarc));
						break;
				}
			}
			AdeCommandManager.Instance.Add(new BatchCommand(commands.ToArray(), "镜像"));
		}

		public override AdeOperationResult TryExecuteOperation()
		{
            if (verticalMirror != null && AdeInputManager.Instance.CheckHotkeyActionPressed(verticalMirror))
            {
                FlipSelectedNotesVertically();
                return true;
            }
			if (AdeInputManager.Instance.CheckHotkeyActionPressed(AdeInputManager.Instance.Hotkeys.Mirror))
			{
				MirrorSelectedNotes();
				return true;
			}
			return false;
		}
		public void ManuallyExecuteOperation()
		{
			AdeOperationManager.Instance.TryExecuteOperation(() =>
			{
				MirrorSelectedNotes();
				return null;
			});
		}
	}
}
