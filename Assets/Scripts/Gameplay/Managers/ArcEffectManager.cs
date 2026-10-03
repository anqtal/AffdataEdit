using Arcade.Audio;
using System;
using System.Collections.Generic;
using Arcade.Compose;
using Arcade.Gameplay.Chart;
using Arcade.Util.Pooling;
using UnityEngine;
using UnityEngine.VFX;

namespace Arcade.Gameplay
{
	public class ArcEffectManager : MonoBehaviour
	{
		public static ArcEffectManager Instance { get; private set; }

		public ArcEffectPlane EffectPlane;
		private void Awake()
		{
			Instance = this;
		}
		private void Start()
		{
			tapNoteEffectPool = new GameObjectPool<ArcTapNoteEffectComponent>(TapNoteJudgeEffect, EffectPlane.transform, 10);
			sfxTapNoteEffectPool = new GameObjectPool<ArcTapNoteEffectComponent>(SfxTapNoteJudgeEffect, EffectPlane.transform, 10);
			for (int i = 0; i < 6; ++i)
			{
				HoldNoteEffectPosition[i] = HoldNoteEffects[i].transform.position;
                ArcLongNoteEffect.DisableAnchor(HoldNoteEffects[i]);
			};
		}

		public GameObject TapNoteJudgeEffect;
		public GameObject SfxTapNoteJudgeEffect;
		public GameObject[] LaneHits = new GameObject[6];
		public VisualEffect[] HoldNoteEffects = new VisualEffect[6];
		public Vector3[] HoldNoteEffectPosition = new Vector3[6];
		public Transform EffectLayer;
		[System.NonSerialized] public BassClip TapAudio, ArcAudio;

		[System.NonSerialized]
		public Dictionary<string, BassClip> SpecialEffectAudios = new Dictionary<string, BassClip>();

		// Long-note particles follow Alpha's ArcLongNoteParticleManager: every hold binds its
		// own pooled particle (GetHoldParticle(ArcHold)), and arcs share one particle per
		// color (GetArcParticle(color)). HoldNoteEffects stay as skin/position templates.
		private sealed class HoldEffect
		{
			public VisualEffect Effect;
			public bool Requested;
		}
		private readonly Dictionary<ArcHold, HoldEffect> holdEffects = new Dictionary<ArcHold, HoldEffect>();
		private readonly Stack<VisualEffect> freeHoldEffects = new Stack<VisualEffect>();
		private readonly List<ArcHold> releasedHolds = new List<ArcHold>();

		private sealed class ArcEffect
		{
			public VisualEffect Effect;
			public readonly List<ArcArcRenderer> Owners = new List<ArcArcRenderer>();
		}
		private readonly Dictionary<int, ArcEffect> arcEffects = new Dictionary<int, ArcEffect>();

		private VisualEffect CreateLongNoteAnchor(string name, VisualEffect template)
		{
			var go = new GameObject(name, typeof(VisualEffect));
			go.layer = template.gameObject.layer;
			go.transform.SetParent(HoldNoteEffects[0].transform.parent, false);
			var effect = go.GetComponent<VisualEffect>();
			effect.visualEffectAsset = template.visualEffectAsset;
			CopyLongNoteSkin(template, effect);
			ArcLongNoteEffect.Get(effect);
			return effect;
		}

		private static void CopyLongNoteSkin(VisualEffect from, VisualEffect to)
		{
			to.SetVector4("StartColor", from.GetVector4("StartColor"));
			to.SetVector4("EndColor", from.GetVector4("EndColor"));
			to.SetTexture("Texture", from.GetTexture("Texture"));
		}

		public void SetHoldNoteEffect(ArcHold note)
		{
			if (!holdEffects.TryGetValue(note, out var state) || !state.Effect)
			{
				VisualEffect effect = null;
				while (freeHoldEffects.Count > 0 && !effect) effect = freeHoldEffects.Pop();
				state = new HoldEffect { Effect = effect ? effect : CreateLongNoteAnchor("Hold hit", HoldNoteEffects[0]) };
				holdEffects[note] = state;
			}
			state.Requested = true;
			state.Effect.transform.position = note.FloatLane.HasValue
				? EffectPlane.GetPositionOnPlane(new Vector2(note.WorldX, 0))
				: EffectPlane.GetPositionOnPlane(HoldNoteEffectPosition[note.Track]);
		}

		public void RemoveFloatHoldNoteEffect(ArcHold note)
		{
			if (!holdEffects.TryGetValue(note, out var state)) return;
			holdEffects.Remove(note);
			ReleaseHoldEffect(state.Effect);
		}

		private void ReleaseHoldEffect(VisualEffect effect)
		{
			if (!effect) return;
			ArcLongNoteEffect.Get(effect).ResetState();
			freeHoldEffects.Push(effect);
		}

		// Arc renderers report their judging state; the color's particle follows the most
		// recent judging arc of that color and keeps emitting while any of them judges.
		public void SetArcEffect(ArcArcRenderer renderer, int color, bool enable)
		{
			if (!arcEffects.TryGetValue(color, out var state))
			{
				state = new ArcEffect();
				arcEffects.Add(color, state);
			}
			state.Owners.RemoveAll(owner => !owner);
			if (state.Owners.Contains(renderer) == enable && (state.Effect || !enable)) return;
			if (enable) state.Owners.Add(renderer);
			else state.Owners.Remove(renderer);
			if (!state.Effect) state.Effect = CreateLongNoteAnchor($"Arc hit {color}", renderer.JudgeEffect);
			ArcArcRenderer current = state.Owners.Count > 0 ? state.Owners[state.Owners.Count - 1] : null;
			var effect = ArcLongNoteEffect.Get(state.Effect);
			if (current)
			{
				// Parent to the arc's judge point so the particle tracks it in the same frame.
				CopyLongNoteSkin(current.JudgeEffect, state.Effect);
				state.Effect.transform.SetParent(current.JudgeEffectTransform, false);
				state.Effect.transform.localPosition = Vector3.zero;
				effect.RefreshSkin();
				effect.SetEmission(true);
			}
			else
			{
				state.Effect.transform.SetParent(HoldNoteEffects[0].transform.parent, true);
				effect.SetEmission(false);
			}
		}

        // Alpha deduplicates the two standard hit sounds independently within 10 ms.
        private const double HitSoundDeduplicationSeconds = 0.010;
        private double lastTapSoundTime = double.NegativeInfinity;
        private double lastArcSoundTime = double.NegativeInfinity;

        public void ResetHitSoundDeduplication()
        {
            lastTapSoundTime = lastArcSoundTime = double.NegativeInfinity;
        }

        private void PlayHitSound(BassClip clip, ref double lastPlayedTime)
        {
            if (!clip) return;
            double now = Time.unscaledTimeAsDouble;
            if (now - lastPlayedTime <= HitSoundDeduplicationSeconds) return;
            BassAudio.PlayOneShot(clip);
            lastPlayedTime = now;
        }

		private GameObjectPool<ArcTapNoteEffectComponent> tapNoteEffectPool;
		private GameObjectPool<ArcTapNoteEffectComponent> sfxTapNoteEffectPool;

		void Update()
		{
			releasedHolds.Clear();
			foreach (var pair in holdEffects)
			{
				var effect = ArcLongNoteEffect.Get(pair.Value.Effect);
				effect.SetEmission(pair.Value.Requested);
				// Return the anchor once its 200 ms stop delay has finished.
				if (!pair.Value.Requested && !effect.Active) releasedHolds.Add(pair.Key);
			}
			foreach (ArcHold hold in releasedHolds)
			{
				ReleaseHoldEffect(holdEffects[hold].Effect);
				holdEffects.Remove(hold);
			}
		}

		public void AddSpecialEffectAudio(string effect, BassClip audio)
		{
			SpecialEffectAudios.Add(effect, audio);
		}

		public void CleanSpecialEffectAudios()
		{
			foreach (var audio in SpecialEffectAudios.Values)
			{
				Destroy(audio);
			}
			SpecialEffectAudios.Clear();
		}

		public void PlayTapNoteEffectAt(Vector2 pos, bool isArc = false, string arcTapEffect = "none")
		{
			if (arcTapEffect.EndsWith("_wav"))
			{
				sfxTapNoteEffectPool.Get((effect) =>
				{
					effect.PlayAt(EffectPlane.GetPositionOnPlane(pos));
				});
			}
			else
			{
				tapNoteEffectPool.Get((effect) =>
				{
					effect.PlayAt(EffectPlane.GetPositionOnPlane(pos));
				});
			}
			if (isArc)
			{
				if (SpecialEffectAudios.ContainsKey(arcTapEffect))
				{
					BassAudio.PlayOneShot(SpecialEffectAudios[arcTapEffect], true);
				}
				else
				{
					PlayArcSound();
				}
			}
			else
			{
				PlayTapSound();
			}
		}
		public void PlayTapSound()
		{
			PlayHitSound(TapAudio, ref lastTapSoundTime);
		}
		public void PlayArcSound()
		{
			PlayHitSound(ArcAudio, ref lastArcSoundTime);
		}
		public void ResetHoldNoteEffect()
		{
			foreach (var state in holdEffects.Values) state.Requested = false;
		}

		public void SetParticleArcColor(Color particleArcStartColor, Color particleArcEndColor)
		{

			foreach (VisualEffect holdEffect in HoldNoteEffects)
			{
				holdEffect.SetVector4("StartColor", particleArcStartColor);
				holdEffect.SetVector4("EndColor", particleArcEndColor);
                holdEffect.GetComponent<ArcLongNoteEffect>()?.RefreshSkin();
			}
			foreach (VisualEffect effect in LongNoteAnchors())
			{
				effect.SetVector4("StartColor", particleArcStartColor);
				effect.SetVector4("EndColor", particleArcEndColor);
				effect.GetComponent<ArcLongNoteEffect>()?.RefreshSkin();
			}
            ArcArcManager.Instance.SetParticleArcColor(particleArcStartColor, particleArcEndColor);
		}

		private IEnumerable<VisualEffect> LongNoteAnchors()
		{
			foreach (var state in holdEffects.Values) if (state.Effect) yield return state.Effect;
			foreach (VisualEffect effect in freeHoldEffects) if (effect) yield return effect;
			foreach (var state in arcEffects.Values) if (state.Effect) yield return state.Effect;
		}

		internal void SetTapEffectTexture(Texture2D particleTap)
		{
			tapNoteEffectPool.Modify(effect =>
			{
				effect.SetTexture(particleTap);
			});
		}

		internal void SetSfxTapEffectTexture(Texture2D particleSfxTap)
		{
			sfxTapNoteEffectPool.Modify(effect =>
			{
				effect.SetTexture(particleSfxTap);
			});
		}

		internal void SetParticleArcTexture(Texture2D texture)
		{

			foreach (VisualEffect holdEffect in HoldNoteEffects)
			{
				holdEffect.SetTexture("Texture", texture);
                holdEffect.GetComponent<ArcLongNoteEffect>()?.RefreshSkin();
			}
			foreach (VisualEffect effect in LongNoteAnchors())
			{
				effect.SetTexture("Texture", texture);
				effect.GetComponent<ArcLongNoteEffect>()?.RefreshSkin();
			}
            ArcArcManager.Instance.SetParticleArcTexture(texture);
		}
	}
}
