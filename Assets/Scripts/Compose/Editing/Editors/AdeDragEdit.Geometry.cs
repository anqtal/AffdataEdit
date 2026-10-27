using System;
using Arcade.Gameplay;
using Arcade.Gameplay.Chart;
using UnityEngine;

namespace Arcade.Compose.Editing
{
    public sealed partial class AdeDragEdit
    {
        private static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

        private static bool TryGroundTiming(Ray ray, int reference, out int timing, out Vector3 point)
        {
            timing = reference; point = default;
            if (!Ground.Raycast(ray, out float distance)) return false;
            point = ray.GetPoint(distance);
            return ArcTimingManager.Instance.TryCalculateChartTimingByPosition(-point.z * 1000,
                AdeTimingEditor.Instance.currentTimingGroup, reference, out timing);
        }

        internal static bool TryChooseEndpoint(ArcNote target, DragMode mode, Vector2 pointer, out bool isEnd)
        {
            isEnd = false;
            if (!(target is ArcLongNote longNote)) return true;
            var camera = AdeCursorManager.Instance.GameplayCamera;
            if (!camera) return false;
            if (mode == DragMode.Position && target is ArcArc arc)
            {
                Vector3 start = EndpointPosition(arc, false), end = EndpointPosition(arc, true);
                if (Mathf.Abs(start.z - end.z) > .0001f && ArcNoteRenderer.TryPickPoint(camera, pointer, arc, out var point))
                    isEnd = Mathf.Abs(point.z - end.z) <= Mathf.Abs(point.z - start.z);
                else
                {
                    Vector3 a = camera.WorldToScreenPoint(start), b = camera.WorldToScreenPoint(end);
                    if (a.z <= 0 && b.z <= 0) return false;
                    isEnd = a.z <= 0 || (b.z > 0 && ((Vector2)b - pointer).sqrMagnitude <= ((Vector2)a - pointer).sqrMagnitude);
                }
                return true;
            }
            var group = ((IHasTimingGroup)target).TimingGroup;
            Ray ray = camera.ScreenPointToRay(pointer);
            if (!Ground.Raycast(ray, out float enter)) return false;
            int reference = (int)(((long)target.Timing + longNote.EndTiming) / 2);
            if (!ArcTimingManager.Instance.TryCalculateChartTimingByPosition(-ray.GetPoint(enter).z * 1000, group, reference, out int timing)) return false;
            isEnd = (long)timing - target.Timing >= (long)longNote.EndTiming - timing;
            return true;
        }

        internal static Vector3 EndpointPosition(ArcNote target, bool isEnd)
        {
            int timing = isEnd ? ((ArcLongNote)target).EndTiming : target.Timing;
            var group = target is ArcArcTap arcTap ? arcTap.Arc.TimingGroup : ((IHasTimingGroup)target).TimingGroup;
            float z = -ArcTimingManager.Instance.CalculatePositionByTiming(timing, group) / 1000;
            if (target is ArcArc arc)
                return new Vector3(ArcAlgorithm.ArcXToWorld(isEnd ? arc.XEnd : arc.XStart), ArcAlgorithm.ArcYToWorld(isEnd ? arc.YEnd : arc.YStart), z);
            if (target is ArcSlide slide) return new Vector3(slide.WorldX(isEnd ? slide.EndCenter : slide.StartCenter), slide.WorldHeight, z);
            if (target is ArcHold hold) return new Vector3(hold.WorldX, 0, z);
            if (target is ArcTap tap) return new Vector3(tap.WorldX, 0, z);
            var skyTap = (ArcArcTap)target;
            float progress = skyTap.Arc.EndTiming == skyTap.Arc.Timing ? 0 : (timing - skyTap.Arc.Timing) / (float)(skyTap.Arc.EndTiming - skyTap.Arc.Timing);
            return new Vector3(ArcAlgorithm.ArcXToWorld(ArcAlgorithm.X(skyTap.Arc.XStart, skyTap.Arc.XEnd, progress, skyTap.Arc.CurveType)),
                ArcAlgorithm.ArcYToWorld(ArcAlgorithm.Y(skyTap.Arc.YStart, skyTap.Arc.YEnd, progress, skyTap.Arc.CurveType)), z);
        }

        private static bool Changed(ArcNote a, ArcNote b)
        {
            if (a.Timing != b.Timing || a is ArcLongNote first && b is ArcLongNote second && first.EndTiming != second.EndTiming) return true;
            if (a is ArcArc arc && b is ArcArc other)
                return arc.XStart != other.XStart || arc.XEnd != other.XEnd || arc.YStart != other.YStart || arc.YEnd != other.YEnd;
            if (a is ArcTap tap && b is ArcTap otherTap) return tap.Track != otherTap.Track || tap.FloatLane != otherTap.FloatLane;
            if (a is ArcHold hold && b is ArcHold otherHold) return hold.Track != otherHold.Track || hold.FloatLane != otherHold.FloatLane;
            return a is ArcSlide slide && b is ArcSlide otherSlide && (slide.StartCenter != otherSlide.StartCenter || slide.EndCenter != otherSlide.EndCenter);
        }
    }
}
