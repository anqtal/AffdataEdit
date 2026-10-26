using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Arcade.Util.UnityExtension;

namespace Arcade.Compose.MarkingMenu
{
	public class MarkingMenuLine : MonoBehaviour
	{
		public static MarkingMenuLine Instance { get; private set; }

		public GameObject KnobPrefab;
		public Image PointerImage;
		public RectTransform Pointer;
		public LineRenderer Line;

		private bool enable;
		private int knobCount;
		private LineRenderer outline;
        private static readonly Color OutlineColor = new Color32(36, 41, 54, 255);
		private List<RectTransform> knobInstances = new List<RectTransform>();

		public bool Enable
		{
			get
			{
				return enable;
			}
			set
			{
				if (enable != value)
				{
					PointerImage.enabled = value;
					Line.enabled = value;
                    outline.enabled = value;
					Initialize();
					enable = value;
				}
			}
		}

		private void Awake()
		{
			Instance = this;
            // Keep the original cyan-to-purple gradient independent of UI accent colors.
            // A wider dark stroke separates it from the white playfield in every theme.
            Line.numCapVertices = Line.numCornerVertices = 8;
            outline = Instantiate(Line, Line.transform.parent, false);
            outline.name = "ContextMenuLineOutline";
            outline.sortingOrder = Line.sortingOrder - 1;
            outline.widthMultiplier = Line.widthMultiplier * 1.5f;
            outline.startColor = outline.endColor = OutlineColor;
            outline.enabled = false;
            StyleKnob(PointerImage, Line.endColor);
		}
		private void Update()
		{
			if (!enable) return;
			UpdatePointer();
		}
		public void Initialize()
		{
			foreach (var knob in knobInstances) Destroy(knob.gameObject);
			knobInstances.Clear();
			knobCount = 0;
			outline.positionCount = Line.positionCount = 1;
            Line.SetPosition(0, Vector3.zero);
            outline.SetPosition(0, Vector3.zero);
		}
		public void AddJoint()
		{
			AddJoint(InputExtension.scaledMousePosition);
		}
		public void AddJoint(Vector2 position)
		{
			RectTransform knob = Instantiate(KnobPrefab, transform).GetComponent<RectTransform>();
			knob.anchoredPosition = position;
			knobInstances.Add(knob);
			StyleKnob(knob.GetComponent<Image>(), Line.startColor);

			Line.SetPosition(knobCount, position);
            outline.SetPosition(knobCount, position);
			knobCount++;
			outline.positionCount = Line.positionCount = knobCount + 1;
		}
		public void RemoveLastJoint()
		{
			if (knobInstances.Count == 0) return;
			RectTransform knob = knobInstances.Last();
			Destroy(knob.gameObject);
			knobInstances.Remove(knob);

			knobCount--;
			outline.positionCount = Line.positionCount = knobCount + 1;
		}

		private static void StyleKnob(Image image, Color color)
        {
            image.color = color;
            var border = image.GetComponent<Shadow>() ?? image.gameObject.AddComponent<Outline>();
            border.effectColor = OutlineColor;
            border.effectDistance = new Vector2(1.25f, -1.25f);
        }

		private void UpdatePointer()
		{
			Vector2 mousePosition = InputExtension.scaledMousePosition;
			Line.SetPosition(knobCount, mousePosition);
            outline.SetPosition(knobCount, mousePosition);
			Pointer.anchoredPosition = mousePosition;
		}
	}
}
