using Arcade.Aff;
using Arcade.Compose;
using Arcade.Gameplay;
using Arcade.Gameplay.Chart;
using System.Collections.Generic;
using UnityEngine;
using Arcade.Aff.Faults;
using System.Linq;
using UnityEngine.UI;
using System.IO;
using System.Globalization;

// The checks follow Arcade Alpha's fault detector, plus Play's ArcTap range check.
namespace Arcade.Aff.Faults
{
	public abstract class Fault
	{
		// One lane in world units; notes closer than this share a lane.
		protected const float LaneWidth = 4.25f;

		public Fault(string Reason)
		{
			this.Reason = Reason;
		}
		public List<ArcEvent> Faults = new List<ArcEvent>();
		// Pairs of an event and the earlier event it overlaps.
		public List<(ArcEvent Current, ArcEvent Other)> Overlaps = new List<(ArcEvent, ArcEvent)>();
		public string Reason;

		public int Count => Faults.Count + Overlaps.Count;

		public abstract void Check(ArcChart chart);

		protected static bool NoInput(IHasTimingGroup e) => e.TimingGroup != null && e.TimingGroup.NoInput;
	}

	public class ShortHoldFault : Fault
	{
		public ShortHoldFault() : base("Hold 持续时间 <= 0ms") { }
		public override void Check(ArcChart chart)
		{
			Faults.AddRange(chart.Holds.Where(h => h.EndTiming <= h.Timing));
		}
	}
	public class NegativeArcFault : Fault
	{
		public NegativeArcFault() : base("Arc 持续时间 < 0ms 且不是黑线") { }
		public override void Check(ArcChart chart)
		{
			Faults.AddRange(chart.Arcs.Where(a => a.EndTiming < a.Timing && !a.IsVoid));
		}
	}
	public class TapOverlapFault : Fault
	{
		public TapOverlapFault() : base("Tap 重叠") { }
		public override void Check(ArcChart chart)
		{
			var taps = chart.Taps.Where(t => !NoInput(t)).ToList();
			for (int i = 0; i < taps.Count - 1; ++i)
				for (int k = i + 1; k < taps.Count; ++k)
					if (taps[i].Timing == taps[k].Timing && Mathf.Approximately(taps[i].WorldX, taps[k].WorldX))
						Overlaps.Add((taps[k], taps[i]));
		}
	}
	public class TapTooCloseFault : Fault
	{
		public TapTooCloseFault() : base("Tap 与前一个 Tap 间隔过小（同一轨道 25ms 内）") { }
		public override void Check(ArcChart chart)
		{
			var taps = chart.Taps.Where(t => !NoInput(t)).ToList();
			for (int i = 0; i < taps.Count - 1; ++i)
				for (int k = i + 1; k < taps.Count; ++k)
					if (Mathf.Abs(taps[i].WorldX - taps[k].WorldX) < LaneWidth && Mathf.Abs(taps[i].Timing - taps[k].Timing) < 25)
						Overlaps.Add((taps[k], taps[i]));
		}
	}
	public class HoldOverlapFault : Fault
	{
		public HoldOverlapFault() : base("Hold 重叠") { }
		public override void Check(ArcChart chart)
		{
			var holds = chart.Holds.Where(h => !NoInput(h)).ToList();
			for (int i = 0; i < holds.Count - 1; ++i)
				for (int k = i + 1; k < holds.Count; ++k)
					if (holds[k].Timing <= holds[i].EndTiming && holds[k].Timing >= holds[i].Timing
						&& Mathf.Abs(holds[i].WorldX - holds[k].WorldX) < LaneWidth)
						Overlaps.Add((holds[k], holds[i]));
		}
	}
	public class ArcTapOverlapFault : Fault
	{
		private const float Epsilon = 0.1f;
		public ArcTapOverlapFault() : base("ArcTap 重叠") { }
		public override void Check(ArcChart chart)
		{
			var ats = new List<(ArcArcTap ArcTap, float X, float Y)>();
			foreach (var arc in chart.Arcs)
			{
				if (NoInput(arc)) continue;
				foreach (var at in arc.ArcTaps)
				{
					float t = arc.EndTiming == arc.Timing ? 0 : 1f * (at.Timing - arc.Timing) / (arc.EndTiming - arc.Timing);
					ats.Add((at, ArcAlgorithm.X(arc.XStart, arc.XEnd, t, arc.CurveType), ArcAlgorithm.Y(arc.YStart, arc.YEnd, t, arc.CurveType)));
				}
			}
			for (int i = 0; i < ats.Count - 1; ++i)
				for (int k = i + 1; k < ats.Count; ++k)
					if (ats[i].ArcTap.Timing == ats[k].ArcTap.Timing
						&& Vector2.Distance(new Vector2(ats[i].X, ats[i].Y), new Vector2(ats[k].X, ats[k].Y)) < Epsilon)
						Overlaps.Add((ats[k].ArcTap, ats[i].ArcTap));
		}
	}
	public class TimingOverlapFault : Fault
	{
		public TimingOverlapFault() : base("Timing 重叠（同一组内时间相同）") { }
		public override void Check(ArcChart chart)
		{
			foreach (var timings in new[] { chart.Timings }.Concat(chart.TimingGroups.Select(g => g.Timings)))
				for (int i = 0; i < timings.Count - 1; ++i)
					for (int k = i + 1; k < timings.Count; ++k)
						if (timings[i].Timing == timings[k].Timing)
							Overlaps.Add((timings[k], timings[i]));
		}
	}
	public class TapHoldOverlapFault : Fault
	{
		public TapHoldOverlapFault() : base("Hold 与 Tap 重叠") { }
		public override void Check(ArcChart chart)
		{
			foreach (var h in chart.Holds)
			{
				if (NoInput(h)) continue;
				foreach (var t in chart.Taps)
					if (!NoInput(t) && t.Timing >= h.Timing && t.Timing <= h.EndTiming && Mathf.Abs(t.WorldX - h.WorldX) < LaneWidth)
						Overlaps.Add((t, h));
			}
		}
	}
	public class ShortLongNoteFault : Fault
	{
		// One frame at 60 fps.
		private const float MinDuration = 16.6667f;
		public ShortLongNoteFault() : base("长条持续时间过短（< 16.67ms）") { }
		public override void Check(ArcChart chart)
		{
			Faults.AddRange(chart.Holds.Where(h => !NoInput(h) && h.EndTiming > h.Timing && h.EndTiming - h.Timing < MinDuration));
			Faults.AddRange(chart.Arcs.Where(a => !NoInput(a) && !a.IsVoid && a.EndTiming > a.Timing && a.EndTiming - a.Timing < MinDuration));
		}
	}
	public class CrossTimingFault : Fault
	{
		// The BPM-changing timings each Arc crosses.
		public readonly Dictionary<ArcEvent, List<ArcTiming>> Crossed = new Dictionary<ArcEvent, List<ArcTiming>>();
		public CrossTimingFault() : base("Arc 跨越变速的 Timing") { }
		public override void Check(ArcChart chart)
		{
			foreach (var a in chart.Arcs)
			{
				float bpm = ArcTimingManager.Instance.CalculateBpmByTiming(a.Timing, a.TimingGroup);
				var crossed = (a.TimingGroup?.Timings ?? chart.Timings)
					.Where(t => t.Timing > a.Timing && t.Timing < a.EndTiming && !Mathf.Approximately(t.Bpm, bpm)).ToList();
				if (crossed.Count == 0) continue;
				Faults.Add(a);
				Crossed[a] = crossed;
			}
		}
	}
	public class ZeroTimeArcTapFault : Fault
	{
		public ZeroTimeArcTapFault() : base("ArcTap 位于持续时间为 0 的 Arc 上") { }
		public override void Check(ArcChart chart)
		{
			Faults.AddRange(chart.Arcs.Where(a => a.EndTiming == a.Timing && a.ArcTaps.Count != 0));
		}
	}
	public class ArcTapOutsideArcFault : Fault
	{
		public ArcTapOutsideArcFault() : base("ArcTap 超出所在 Arc 的时间范围") { }
		public override void Check(ArcChart chart)
		{
			foreach (var a in chart.Arcs)
			{
				int start = Mathf.Min(a.Timing, a.EndTiming), end = Mathf.Max(a.Timing, a.EndTiming);
				Faults.AddRange(a.ArcTaps.Where(at => at.Timing < start || at.Timing > end));
			}
		}
	}
	public class MissingSfxFault : Fault
	{
		public MissingSfxFault() : base("Arc 引用的音效文件不存在") { }
		public override void Check(ArcChart chart)
		{
			var loaded = ArcEffectManager.Instance.SpecialEffectAudios;
			Faults.AddRange(chart.Arcs.Where(a => a.Effect != null && a.Effect.EndsWith("_wav") && !loaded.ContainsKey(a.Effect)));
		}
	}
}



namespace Arcade.Compose
{
	public class AdeFaultDetector : MonoBehaviour
	{
		public Text Status;
		public void OnInvoke()
		{
			if (!ArcGameplayManager.Instance.IsLoaded)
			{
				AdeToast.Instance.Show("未加载工程");
				return;
			}

			Status.text = "请点击检查";

			ArcChart chart = ArcGameplayManager.Instance.Chart;
			Fault[] checks = {
				new ShortHoldFault(), new TapOverlapFault(), new TapTooCloseFault(), new HoldOverlapFault(),
				new ArcTapOverlapFault(), new TimingOverlapFault(), new TapHoldOverlapFault(), new NegativeArcFault(),
				new ShortLongNoteFault(), new CrossTimingFault(), new ZeroTimeArcTapFault(), new ArcTapOutsideArcFault(),
				new MissingSfxFault() };
			string path = AdeProjectManager.Instance.CurrentProjectFolder + "/Arcade/ChartFault.txt";
			int offset = ArcGameplayManager.Instance.ChartAudioOffset;
			int count = 0;
			using (var sw = new StreamWriter(new FileStream(path, FileMode.Create)))
			{
				sw.WriteLine("错误报告");
				sw.WriteLine($"\t难度：{AdeProjectManager.Instance.CurrentDifficulty.ToString(CultureInfo.InvariantCulture)}.aff");
				sw.WriteLine("\t谱面时间为 aff 中的时间，便于在 aff 中搜索；音频时间加上了音频偏移，便于在时间框中跳转");
				sw.WriteLine();
				foreach (var c in checks)
				{
					c.Check(chart);
					if (c.Count == 0) continue;
					sw.WriteLine(c.Reason);
					foreach (var (current, other) in c.Overlaps)
					{
						sw.WriteLine($"\t与之重叠：{Statement(other)}");
						WriteEvent(sw, current, offset);
					}
					foreach (var e in c.Faults)
					{
						if (c is CrossTimingFault cross)
							foreach (var t in cross.Crossed[e])
								sw.WriteLine($"\t跨越：{Statement(t)}");
						WriteEvent(sw, e, offset);
					}
					count += c.Count;
				}
			}
			Status.text = $"检查完成，共 {count.ToString(CultureInfo.InvariantCulture)} 个错误";
			if (count > 0) Arcade.Util.Shell.FileBrowser.OpenExplorer(path);
		}

		private static void WriteEvent(StreamWriter sw, ArcEvent e, int offset)
		{
			int group = (e as IHasTimingGroup)?.TimingGroup?.Id ?? 0;
			if (group != 0) sw.WriteLine($"\tTiming Group：{group.ToString(CultureInfo.InvariantCulture)}");
			sw.WriteLine($"\t谱面时间：{e.Timing.ToString(CultureInfo.InvariantCulture)}\t音频时间：{(e.Timing + offset).ToString(CultureInfo.InvariantCulture)}");
			sw.WriteLine($"\t语句：{Statement(e)}");
			sw.WriteLine();
		}

		// The event as written in the aff file; an ArcTap is quoted with its Arc.
		private static string Statement(ArcEvent e)
		{
			if (e is ArcArcTap at)
				return $"arctap({at.Timing.ToString(CultureInfo.InvariantCulture)})，所在 Arc：{Statement(at.Arc)}";
			return e is IIntoRawItem item ? ArcaeaFileFormat.FormatItem(item.IntoRawItem()) : e.GetType().Name;
		}
	}
}
