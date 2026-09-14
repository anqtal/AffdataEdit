using System.Reflection;
using Arcade.Gameplay;
using Arcade.Gameplay.Chart;
using NUnit.Framework;
using UnityEngine;

public class ArcTimingVelocityTests
{
    [TestCase(30, 100, 30000)]
    [TestCase(31, 100, 31000)]
    [TestCase(32, 100, 32000)]
    [TestCase(195, 100, 195000)]
    [TestCase(30, 200, 60000)]
    [TestCase(30, -100, -30000)]
    public void OneSecondTravelMatchesAlpha(int setting, float bpm, float expected)
    {
        var go = new GameObject("Timing velocity test");
        go.SetActive(false);
        try
        {
            var manager = go.AddComponent<ArcTimingManager>();
            // Avoid the UI/rebuild side effects of the public setting setter.
            typeof(ArcTimingManager).GetField("settingVelocity", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, setting);
            manager.BaseBpm = 100;
            manager.Timings.Add(new ArcTiming { Timing = 0, Bpm = bpm });
            Assert.That(manager.CalculatePositionByTimingAndStart(0, 1000, null), Is.EqualTo(expected).Within(0.01f));
            Assert.That(manager.CalculatePositionByTimingAndStart(1000, 0, null), Is.EqualTo(-expected).Within(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
