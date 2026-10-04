using System.Collections.Generic;
using System.IO;
using Arcade.Audio;
using Arcade.Gameplay.Chart;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Arcade.Gameplay
{
	// AffdataPlay's guide sound (正解音): a cue at the start of every inputtable tap,
	// hold, slide, arctap and arc that does not continue the previous arc. It plays
	// only during playback; seeking skips the cues it passed.
	public sealed class ArcGuideSound : MonoBehaviour
	{
		private const string EditorSceneName = "ArcEditor";
		private const int SeekForwardMs = 1000, SeekBackwardMs = 50, DuplicateMs = 1;
		private const float ArcPositionEpsilon = 0.0001f;

		private readonly List<int> timings = new List<int>();
		private readonly HashSet<int> collected = new HashSet<int>();
		private readonly List<ArcArc> arcsByEnd = new List<ArcArc>();
		private BassClip clip;
		private int next;
		private int lastTiming = int.MinValue;
		private bool wasPlaying;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Bootstrap()
		{
			SceneManager.sceneLoaded += (scene, mode) =>
			{
				if (scene.name == EditorSceneName)
					new GameObject(nameof(ArcGuideSound)).AddComponent<ArcGuideSound>();
			};
		}

		private void Start()
		{
			string path = Path.Combine(Application.streamingAssetsPath, "Audio", "GuideHit.wav");
			if (File.Exists(path)) clip = BassClip.Load(path);
			else Debug.LogWarning("Guide sound missing: " + path);
		}

		private void OnDestroy()
		{
			if (clip) Destroy(clip);
		}

		private void Update()
		{
			var game = ArcGameplayManager.Instance;
			if (!clip || !game || !game.IsLoaded || game.Chart == null) return;
			int timing = game.ChartTiming;
			bool playing = game.IsPlaying;
			// Notes are edited while paused, so the cue list is rebuilt when playback starts.
			if (playing && !wasPlaying)
			{
				Rebuild();
				SkipTo(timing);
			}
			wasPlaying = playing;
			if (!playing || BassAudio.GuideVolume <= 0)
			{
				SkipTo(timing);
				return;
			}
			if (timing < lastTiming - SeekBackwardMs || timing > lastTiming + SeekForwardMs) SkipTo(timing);
			bool play = false;
			while (next < timings.Count && timings[next] <= timing)
			{
				play = true;
				next++;
			}
			if (play) BassAudio.PlayOneShot(clip, BassAudio.Bus.Guide);
			lastTiming = timing;
		}

		private void SkipTo(int timing)
		{
			int index = timings.BinarySearch(timing + 1);
			next = index < 0 ? ~index : index;
			lastTiming = timing;
		}

		private void Rebuild()
		{
			collected.Clear();
			foreach (ArcTap tap in ArcTapNoteManager.Instance.Taps)
				if (!tap.NoInput()) collected.Add(tap.Timing);
			foreach (ArcHold hold in ArcHoldNoteManager.Instance.Holds)
				if (!hold.NoInput()) collected.Add(hold.Timing);
			foreach (ArcSlide slide in ArcSlideManager.Instance.Slides)
				if (slide.IsGroupHead && !slide.NoInput() && slide.EndTiming > slide.Timing) collected.Add(slide.Timing);
			arcsByEnd.Clear();
			foreach (ArcArc arc in ArcArcManager.Instance.Arcs)
			{
				if (arc.NoInput()) continue;
				foreach (ArcArcTap arctap in arc.ArcTaps) collected.Add(arctap.Timing);
				if (arc.IsVariousSizedArctap) collected.Add(arc.Timing);
				arcsByEnd.Add(arc);
			}
			arcsByEnd.Sort((a, b) => a.EndTiming.CompareTo(b.EndTiming));
			foreach (ArcArc arc in arcsByEnd)
				if (StartsChain(arc)) collected.Add(arc.Timing);

			timings.Clear();
			timings.AddRange(collected);
			timings.Sort();
			// Cues within 1 ms of the previous kept cue are merged.
			int write = 0;
			for (int read = 0; read < timings.Count; read++)
			{
				if (write > 0 && timings[read] - timings[write - 1] <= DuplicateMs) continue;
				timings[write++] = timings[read];
			}
			timings.RemoveRange(write, timings.Count - write);
		}

		// A solid arc gets a cue unless a same-colored arc ends where it starts.
		private bool StartsChain(ArcArc arc)
		{
			if (arc.IsVoid || arc.EndTiming <= arc.Timing) return false;
			int index = LowerBound(arc.Timing - 1);
			for (int i = index; i < arcsByEnd.Count && arcsByEnd[i].EndTiming <= arc.Timing + 1; i++)
			{
				ArcArc previous = arcsByEnd[i];
				if (previous != arc && !previous.IsVoid && previous.EndTiming > previous.Timing
					&& previous.Color == arc.Color
					&& Mathf.Abs(previous.XEnd - arc.XStart) <= ArcPositionEpsilon
					&& Mathf.Abs(previous.YEnd - arc.YStart) <= ArcPositionEpsilon)
					return false;
			}
			return true;
		}

		private int LowerBound(int endTiming)
		{
			int low = 0, high = arcsByEnd.Count;
			while (low < high)
			{
				int mid = (low + high) / 2;
				if (arcsByEnd[mid].EndTiming < endTiming) low = mid + 1;
				else high = mid;
			}
			return low;
		}
	}
}
