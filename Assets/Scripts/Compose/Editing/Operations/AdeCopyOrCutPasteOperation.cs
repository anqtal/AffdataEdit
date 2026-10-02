using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Arcade.Gameplay.Chart;
using Arcade.Compose.MarkingMenu;
using Arcade.Gameplay;
using Arcade.Compose.Command;
using System.Linq;
using Arcade.Compose.Editing;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using Arcade.Util.UniTaskHelper;

namespace Arcade.Compose.Operation
{
	public class AdeCopyOrCutPasteOperation : AdeOperation
	{
		public static AdeCopyOrCutPasteOperation Instance { get; private set; }

		public MarkingMenuItem CopyItem;
		public MarkingMenuItem CutItem;
        private MarkingMenuItem horizontalCutItem;

		public override bool IsOnlyMarkingMenu => false;
		public override MarkingMenuItem[] MarkingMenuItems
		{
			get
			{
				if (!ArcGameplayManager.Instance.IsLoaded) return null;
				if (AdeCursorManager.Instance == null) return null;
				if (AdeSelectionManager.Instance.SelectedNotes.Count == 0) return null;
				return horizontalCutItem && AdeSelectionManager.Instance.SelectedNotes.Exists(note => note is ArcArc arc && arc.IsVoid)
                    ? new[] { CopyItem, CutItem, horizontalCutItem } : new[] { CopyItem, CutItem };
			}
		}

		private void Awake()
		{
			Instance = this;
		}

        private void Start()
        {
            horizontalCutItem = Instantiate(CutItem, CutItem.transform.parent);
            horizontalCutItem.name = "Horizontal cut";
            horizontalCutItem.StartupText = "水平剪切";
            horizontalCutItem.HasSubMenu = false;
            horizontalCutItem.SubItems = new MarkingMenuItem[0];
            horizontalCutItem.OnHangOver = new UnityEvent();
            horizontalCutItem.OnConfirmed = new UnityEvent();
            horizontalCutItem.OnConfirmed.AddListener(ManuallyExecuteHorizontalCut);
            horizontalCutItem.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (horizontalCutItem) Destroy(horizontalCutItem.gameObject);
        }

        public void ManuallyExecuteHorizontalCut()
        {
            AdeOperationManager.Instance.TryExecuteOperation(() =>
            {
                var cancellation = new CancellationTokenSource();
                return new AdeOngoingOperation
                {
                    task = ExecuteHorizontalCut(cancellation.Token).WithExceptionLogger(),
                    cancellation = cancellation,
                };
            });
        }

        private async UniTask ExecuteHorizontalCut(CancellationToken cancellationToken)
        {
            if (!ArcGameplayManager.Instance.IsLoaded) return;
            var selection = AdeSelectionManager.Instance.SelectedNotes.ToArray();
            var arcs = selection.OfType<ArcArc>().Where(arc => arc.IsVoid)
                .OrderBy(arc => arc.Timing).ToArray();
            if (arcs.Length == 0) return;

            var originals = arcs.Select(arc => (ArcArc)arc.Clone()).ToArray();
            var moved = arcs.Select(arc => (ArcArc)arc.Clone()).ToArray();
            var command = new BatchCommand(arcs.Select((arc, i) =>
                (ICommand)new EditArcEventCommand(arc, moved[i])).ToArray(), "水平剪切");
            int cursorTiming = Mathf.Max(arcs[0].Timing, ArcGameplayManager.Instance.ChartTiming);
            float offset = 0;
            AdeSelectionManager.Instance.DeselectAllNotes();
            AdeCommandManager.Instance.Prepare(command);
            AdeToast.Instance.Show("水平剪切：以最早黑线的起点定位，左键确认，Esc 取消");
            try
            {
                Action<Vector2> updatePosition = point =>
                {
                    float nextOffset = point.x - originals[0].XStart;
                    if (Mathf.Approximately(nextOffset, offset)) return;
                    offset = nextOffset;
                    for (int i = 0; i < moved.Length; i++)
                    {
                        // Apply one shared offset from the original positions to preserve the shape.
                        moved[i].XStart = originals[i].XStart + offset;
                        moved[i].XEnd = originals[i].XEnd + offset;
                    }
                    command.Do();
                };
                var coordinate = await AdeCursorManager.Instance.SelectCoordinate(cursorTiming,
                    Progress.Create(updatePosition), cancellationToken);
                updatePosition(coordinate);
                if (Mathf.Approximately(offset, 0)) AdeCommandManager.Instance.Cancel();
                else AdeCommandManager.Instance.Commit();
            }
            catch
            {
                AdeCommandManager.Instance.Cancel();
                throw;
            }
            finally
            {
                foreach (var note in selection) AdeSelectionManager.Instance.SelectNote(note);
            }
        }

		private async UniTask ExecuteCopyOrCut(bool isCut, CancellationToken cancellationToken)
		{
			var oldNotes = AdeSelectionManager.Instance.SelectedNotes.ToArray();
			AdeSelectionManager.Instance.DeselectAllNotes();
			if (oldNotes.Length == 0) return;
			List<ICommand> commands = new List<ICommand>();
			List<ArcNote> newNotes = new List<ArcNote>();
			foreach (var oldNote in oldNotes)
			{
				ArcEvent newNote = oldNote.Clone();
				if (newNote is ArcArcTap)
				{
					if (oldNotes.Contains((oldNote as ArcArcTap).Arc))
					{
						continue;
					}
					commands.Add(new AddArcTapCommand((oldNote as ArcArcTap).Arc, newNote as ArcArcTap));
					if (isCut)
					{
						commands.Add(new RemoveArcTapCommand((oldNote as ArcArcTap).Arc, oldNote as ArcArcTap));
					}
				}
				else
				{
					commands.Add(new AddArcEventCommand(newNote));
					if (isCut)
					{
						commands.Add(new RemoveArcEventCommand(oldNote));
					}
					if (newNote is ArcArc)
					{
						foreach (var at in (newNote as ArcArc).ArcTaps)
							newNotes.Add(at);
					}
				}
				newNotes.Add(newNote as ArcNote);
			}
			AdeCommandManager.Instance.Prepare(new BatchCommand(commands.ToArray(), isCut ? "剪切" : "复制"));

			try
			{
				Action<int> updateTiming = (int timing) =>
				{
					int beginTiming = newNotes.Min((n) => n.Timing);
					if (beginTiming != timing)
					{
						foreach (var n in newNotes)
						{
							n.Judged = false;
							int diff = n.Timing - beginTiming;
							switch (n)
							{
								case ArcLongNote note:
									int duration = note.EndTiming - note.Timing;
									note.Timing = timing + diff;
									note.EndTiming = timing + duration + diff;
									note.Judging = false;
									(note as ArcArc)?.Rebuild();
									if (note is ArcArc)
									{
										ArcArcManager.Instance.CalculateArcRelationship();
									}
									(note as ArcArc)?.CalculateJudgeTimings();
									(note as ArcHold)?.CalculateJudgeTimings();
									break;
								case ArcArcTap note:
									note.RemoveArcTapConnection();
									note.Timing = timing + diff;
									note.Relocate();
									break;
								case ArcTap note:
									note.Timing = timing + diff;
									note.SetupArcTapConnection();
									break;
								default:
									n.Timing = timing + diff;
									break;
							}
						}
					}
				};
				while (true)
				{
					var newTiming = await AdeCursorManager.Instance.SelectTiming(Progress.Create(updateTiming), cancellationToken);
					updateTiming(newTiming);
					bool hasIllegalArcTap = false;
					foreach (var n in newNotes)
					{
						switch (n)
						{
							case ArcArcTap note:
								if (note.Arc.Timing > note.Timing || note.Arc.EndTiming < note.Timing)
								{
									hasIllegalArcTap = true;
								}
								break;
							default:
								break;
						}
					}

					if (!hasIllegalArcTap)
					{
						break;
					}
					else
					{
						AdeToast.Instance.Show("粘贴的 Arctap 中有一部分超出了所在 Arc 的时间范围，无法粘贴");
					}
				}
			}
			catch (OperationCanceledException ex)
			{
				AdeCommandManager.Instance.Cancel();
				throw ex;
			}
			AdeCommandManager.Instance.Commit();
			if (AdeInputManager.Instance.Inputs.MultipleSelection.IsPressed())
			{
				foreach (var note in newNotes)
				{
					AdeSelectionManager.Instance.SelectNote(note);
				}
			}
		}

		public override AdeOperationResult TryExecuteOperation()
		{
			if (AdeInputManager.Instance.CheckHotkeyActionPressed(AdeInputManager.Instance.Hotkeys.Copy))
			{
				var cancellation = new CancellationTokenSource();
				return AdeOperationResult.FromOngoingOperation(new AdeOngoingOperation
				{
					task = ExecuteCopyOrCut(false, cancellation.Token).WithExceptionLogger(),
					cancellation = cancellation,
				});
			}
			else if (AdeInputManager.Instance.CheckHotkeyActionPressed(AdeInputManager.Instance.Hotkeys.Cut))
			{
				var cancellation = new CancellationTokenSource();
				return AdeOperationResult.FromOngoingOperation(new AdeOngoingOperation
				{
					task = ExecuteCopyOrCut(true, cancellation.Token).WithExceptionLogger(),
					cancellation = cancellation,
				});
			}
			return false;
		}

		public void ManuallyExecuteCopyOperation()
		{
			AdeOperationManager.Instance.TryExecuteOperation(() =>
			{
				var cancellation = new CancellationTokenSource();
				return new AdeOngoingOperation
				{
					task = ExecuteCopyOrCut(false, cancellation.Token).WithExceptionLogger(),
					cancellation = cancellation,
				};
			});
		}

		public void ManuallyExecuteCutOperation()
		{
			AdeOperationManager.Instance.TryExecuteOperation(() =>
			{
				var cancellation = new CancellationTokenSource();
				return new AdeOngoingOperation
				{
					task = ExecuteCopyOrCut(true, cancellation.Token).WithExceptionLogger(),
					cancellation = cancellation,
				};
			});
		}
	}
}
