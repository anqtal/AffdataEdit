using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Arcade.Gameplay.Chart;
using System.Globalization;

namespace Arcade.Gameplay
{
	public class ArcScoreManager : MonoBehaviour
	{
		public static ArcScoreManager Instance { get; private set; }
		private void Awake()
		{
			Instance = this;
		}

		public Text ScoreText;
		public Text ComboText;

		private int combo = 0;

		// Score roll shared by AffdataPlay (ScoreDisplay) and Arcade Alpha (SpinCountNText):
		// a 500 ms linear roll while playing; seeking or a lower score shows it at once.
		private const float ScoreRollSeconds = 0.5f;
		private double rollStart, rollTarget, rollValue;
		private float rollElapsed = ScoreRollSeconds;

		private void Update()
		{
			ComboText.text = "";
			ScoreText.text = "00000000";
			if (!ArcGameplayManager.Instance.IsLoaded || ArcGameplayManager.Instance.Chart == null)
			{
				SetRoll(0);
				return;
			}
			int score = RollScore(CalculateScore(ArcGameplayManager.Instance.ChartTiming));
			ScoreText.text = score.ToString("D8", CultureInfo.InvariantCulture);

			if (combo < 2) ComboText.text = "";
			else ComboText.text = combo.ToString(CultureInfo.InvariantCulture);
		}

		private int RollScore(int target)
		{
			if (target != rollTarget)
			{
				if (!ArcGameplayManager.Instance.IsPlaying || target < rollValue) SetRoll(target);
				else
				{
					// Continue from the shown value so a new target mid-roll stays smooth.
					rollStart = rollValue;
					rollTarget = target;
					rollElapsed = 0;
				}
			}
			rollElapsed = Mathf.Min(ScoreRollSeconds, rollElapsed + Time.unscaledDeltaTime);
			rollValue = rollStart + (rollTarget - rollStart) * (rollElapsed / ScoreRollSeconds);
			return (int)Math.Round(rollValue);
		}

		private void SetRoll(double value)
		{
			rollStart = rollTarget = rollValue = value;
			rollElapsed = ScoreRollSeconds;
		}

		private double CalculateSingleScore()
		{
			int total = 0;
			total += ArcTapNoteManager.Instance.Taps.Where((tap) => !tap.NoInput()).Count();
			foreach (var hold in ArcHoldNoteManager.Instance.Holds)
			{
				if (hold.NoInput())
				{
					continue;
				}
				total += hold.JudgeTimings.Count;
			}
			foreach (var arc in ArcArcManager.Instance.Arcs)
			{
				if (arc.NoInput() || arc.Designant)
				{
					continue;
				}
				total += arc.JudgeTimings.Count;
				total += arc.ArcTaps.Count;
				if (arc.IsVariousSizedArctap)
				{
					total += 1;
				}
			}
			foreach (var slide in ArcSlideManager.Instance.Slides)
				total += CountSlideJudgements(slide, slide.EndTiming);
			if (total == 0) return 0;
			return 10000000d / total;
		}
		private int CalculateCombo(int timing)
		{
			int note = 0;
			foreach (var tap in ArcTapNoteManager.Instance.Taps)
			{
				if (tap.NoInput())
				{
					continue;
				}
				if (tap.Timing <= timing)
				{
					note++;
				}
			}
			foreach (var hold in ArcHoldNoteManager.Instance.Holds)
			{
				if (hold.NoInput())
				{
					continue;
				}
				if (hold.Timing <= timing)
				{
					foreach (float t in hold.JudgeTimings)
					{
						if (t <= timing)
						{
							note++;
						}
					}
				}
			}
			foreach (var arc in ArcArcManager.Instance.Arcs)
			{
				if (arc.NoInput() || arc.Designant)
				{
					continue;
				}
				if (arc.Timing > timing) continue;
				foreach (float t in arc.JudgeTimings)
				{
					if (t <= timing)
					{
						note++;
					}
				}
				foreach (var arctap in arc.ArcTaps)
				{
					if (arctap.Timing <= timing)
					{
						note++;
					}
				}
				if (arc.IsVariousSizedArctap)
				{
					if (arc.Timing <= timing)
					{
						note++;
					}
				}
			}
			foreach (var slide in ArcSlideManager.Instance.Slides)
				note += CountSlideJudgements(slide, timing);
			return note;
		}
		private static int CountSlideJudgements(ArcSlide slide, int timing)
		{
			return slide.CountJudgements(timing,
				ArcTimingManager.Instance.CalculateBpmByTiming(slide.Timing, slide.TimingGroup),
				ArcGameplayManager.Instance.TimingPointDensityFactor);
		}
		private int CalculateScore(int timing)
		{
			double single = CalculateSingleScore();
			if (single == 0)
			{
				combo = 0;
				return 0;
			}
			combo = CalculateCombo(timing);
			return (int)Math.Round((combo * single + combo));
		}
	}
}
