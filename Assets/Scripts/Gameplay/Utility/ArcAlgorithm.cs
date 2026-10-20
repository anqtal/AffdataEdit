using Affdata.Core.Geometry;
using CoreCurveType = Affdata.Core.Geometry.ArcCurveType;
using ArcCurveType = Arcade.Gameplay.Chart.ArcCurveType;

namespace Arcade.Gameplay
{
	public static class ArcAlgorithm
	{
		public static float ArcXToWorld(float x)
		{
			return ArcCoordinates.ArcXToWorld(x);
		}
		public static float ArcYToWorld(float y)
		{
			return ArcCoordinates.ArcYToWorld(y);
		}
		public static float WorldXToArc(float x)
		{
			return ArcCoordinates.WorldXToArc(x);
		}
		public static float WorldYToArc(float y)
		{
			return ArcCoordinates.WorldYToArc(y);
		}
		public static float S(float start, float end, float t)
		{
			return ArcCurve.S(start, end, t);
		}
		public static float O(float start, float end, float t)
		{
			return ArcCurve.O(start, end, t);
		}
		public static float I(float start, float end, float t)
		{
			return ArcCurve.I(start, end, t);
		}
		public static float B(float start, float end, float t)
		{
			return ArcCurve.B(start, end, t);
		}

		public static float X(float start, float end, float t, ArcCurveType type)
		{
			return ArcCurve.X(start, end, t, (CoreCurveType)type);
		}
		public static float Y(float start, float end, float t, ArcCurveType type)
		{
			return ArcCurve.Y(start, end, t, (CoreCurveType)type);
		}

		public static float Qi(float value)
		{
			return ArcCurve.Qi(value);
		}
		public static float Qo(float value)
		{
			return ArcCurve.Qo(value);
		}
	}
}
