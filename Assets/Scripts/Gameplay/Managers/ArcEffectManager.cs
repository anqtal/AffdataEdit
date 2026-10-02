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
                ArcLongNoteEffect.Get(HoldNoteEffects[i]);
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

		private sealed class FloatHoldEffect
        {
            public VisualEffect Effect;
            public bool Requested;
        }
        private readonly Dictionary<ArcHold, FloatHoldEffect> floatHoldEffects = new Dictionary<ArcHold, FloatHoldEffect>();

        public void SetFloatHoldNoteEffect(ArcHold note)
        {
            if (!floatHoldEffects.TryGetValue(note, out var state))
            {
                var go = new GameObject("FloatLane hold hit", typeof(VisualEffect));
                go.layer = HoldNoteEffects[0].gameObject.layer;
                go.transform.SetParent(HoldNoteEffects[0].transform.parent, false);
                var effect = go.GetComponent<VisualEffect>();
                effect.visualEffectAsset = HoldNoteEffects[0].visualEffectAsset;
                effect.SetVector4("StartColor", HoldNoteEffects[0].GetVector4("StartColor"));
                effect.SetVector4("EndColor", HoldNoteEffects[0].GetVector4("EndColor"));
                effect.SetTexture("Texture", HoldNoteEffects[0].GetTexture("Texture"));
                ArcLongNoteEffect.Get(effect);
                effect.name = "FloatLane hold hit";
                effect.Stop();
                state = new FloatHoldEffect { Effect = effect };
                floatHoldEffects.Add(note, state);
            }
            state.Requested = true;
            state.Effect.transform.position = EffectPlane.GetPositionOnPlane(new Vector2(note.WorldX, 0));
        }

        public void RemoveFloatHoldNoteEffect(ArcHold note)
        {
            if (!floatHoldEffects.TryGetValue(note, out var state)) return;
            if (state.Effect) Destroy(state.Effect.gameObject);
            floatHoldEffects.Remove(note);
        }

        private bool[] holdEffectStatus = new bool[6];
		private GameObjectPool<ArcTapNoteEffectComponent> tapNoteEffectPool;
		private GameObjectPool<ArcTapNoteEffectComponent> sfxTapNoteEffectPool;

		void Update()
        {
            foreach (var state in floatHoldEffects.Values)
            {
                ArcLongNoteEffect.Get(state.Effect).SetEmission(state.Requested);
            }
			for (int track = 0; track < 6; track++)
			{
				HoldNoteEffects[track].transform.position = EffectPlane.GetPositionOnPlane(HoldNoteEffectPosition[track]);
				bool show = holdEffectStatus[track];
                ArcLongNoteEffect.Get(HoldNoteEffects[track]).SetEmission(show);
			}
		}
		public void SetHoldNoteEffect(int track, bool show)
		{
			if (holdEffectStatus[track] != show)
			{
				holdEffectStatus[track] = show;
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
					BassAudio.PlayOneShot(ArcAudio);
				}
			}
			else
			{
				BassAudio.PlayOneShot(TapAudio);
			}
		}
		public void PlayTapSound()
		{
			BassAudio.PlayOneShot(TapAudio);
		}
		public void PlayArcSound()
		{
			BassAudio.PlayOneShot(ArcAudio);
		}
		public void ResetHoldNoteEffect()
		{
			for (int i = 0; i < 6; ++i) SetHoldNoteEffect(i, false);
            foreach (var state in floatHoldEffects.Values) state.Requested = false;
		}

		public void SetParticleArcColor(Color particleArcStartColor, Color particleArcEndColor)
		{

			foreach (VisualEffect holdEffect in HoldNoteEffects)
			{
				holdEffect.SetVector4("StartColor", particleArcStartColor);
				holdEffect.SetVector4("EndColor", particleArcEndColor);
                holdEffect.GetComponent<ArcLongNoteEffect>()?.RefreshSkin();
			}
			foreach (var state in floatHoldEffects.Values)
            {
                state.Effect.SetVector4("StartColor", particleArcStartColor);
                state.Effect.SetVector4("EndColor", particleArcEndColor);
                state.Effect.GetComponent<ArcLongNoteEffect>()?.RefreshSkin();
            }
            ArcArcManager.Instance.SetParticleArcColor(particleArcStartColor, particleArcEndColor);
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
			foreach (var state in floatHoldEffects.Values) { state.Effect.SetTexture("Texture", texture); state.Effect.GetComponent<ArcLongNoteEffect>()?.RefreshSkin(); }
            ArcArcManager.Instance.SetParticleArcTexture(texture);
		}
	}
}
