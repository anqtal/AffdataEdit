using Arcade.Compose;
using NUnit.Framework;
using UnityEngine;

public class AdeScreenResolutionTests
{
	[Test]
	public void WindowSizeUsesSelectedResolutionWithoutDpiScaling()
	{
		Assert.AreEqual(new Vector2Int(1280, 720),
			AdeScreenResolution.GetWindowedSize("1280x720", new RectInt(0, 0, 2560, 1400)));
	}

	[TestCase("1920x1080", 1280, 760)]
	[TestCase("1280x960", 1440, 850)]
	[TestCase("1920x1080", 800, 600)]
	public void OversizedWindowFitsWorkAreaAndPreservesAspectRatio(string resolution, int width, int height)
	{
		Vector2Int size = AdeScreenResolution.GetWindowedSize(resolution, new RectInt(0, 0, width, height));
		Assert.That(size.x, Is.InRange(1, width - 32));
		Assert.That(size.y, Is.InRange(1, height - 64));
		string[] dimensions = resolution.Split('x');
		float aspect = float.Parse(dimensions[0]) / float.Parse(dimensions[1]);
		Assert.That((float)size.x / size.y, Is.EqualTo(aspect).Within(0.01f));
	}

	[TestCase(null)]
	[TestCase("")]
	[TestCase("broken")]
	[TestCase("0x720")]
	[TestCase("-1x720")]
	[TestCase("999999999999x720")]
	public void InvalidPreferenceFallsBackToDefault(string resolution)
	{
		Assert.AreEqual(new Vector2Int(1280, 720),
			AdeScreenResolution.GetWindowedSize(resolution, new RectInt(0, 0, 1920, 1080)));
	}

	[Test]
	public void UnavailableWorkAreaKeepsRequestedSize()
	{
		Assert.AreEqual(new Vector2Int(800, 450),
			AdeScreenResolution.GetWindowedSize("800x450", new RectInt()));
	}
}
