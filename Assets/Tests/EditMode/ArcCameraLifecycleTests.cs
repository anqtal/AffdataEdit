using Arcade.Gameplay;
using NUnit.Framework;
using UnityEngine;

public class ArcCameraLifecycleTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void ResetCameraToleratesDestroyedSceneDependencies(bool destroyLabel)
    {
        var host = new GameObject("Camera lifecycle test");
        host.SetActive(false);
        var cameraObject = new GameObject("Gameplay camera");
        var labelObject = new GameObject("Sky input label");
        try
        {
            var manager = host.AddComponent<ArcCameraManager>();
            manager.GameplayCamera = cameraObject.AddComponent<Camera>();
            manager.SkyInputLabel = labelObject.transform;
            Object.DestroyImmediate(destroyLabel ? labelObject : cameraObject);
            Assert.DoesNotThrow(manager.ResetCamera);
        }
        finally
        {
            if (cameraObject) Object.DestroyImmediate(cameraObject);
            if (labelObject) Object.DestroyImmediate(labelObject);
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void ResetCameraStillRestoresLiveCameraAndLabel()
    {
        var host = new GameObject("Camera reset test");
        host.SetActive(false);
        var cameraObject = new GameObject("Gameplay camera");
        var labelObject = new GameObject("Sky input label");
        try
        {
            var manager = host.AddComponent<ArcCameraManager>();
            manager.GameplayCamera = cameraObject.AddComponent<Camera>();
            manager.GameplayCamera.aspect = 16f / 9f;
            manager.SkyInputLabel = labelObject.transform;
            manager.IsReset = false;
            cameraObject.transform.position = Vector3.one * 100;
            labelObject.transform.localPosition = Vector3.one * 100;
            manager.ResetCamera();
            Assert.That(manager.IsReset, Is.True);
            Assert.That(cameraObject.transform.position.y, Is.EqualTo(9));
            Assert.That(labelObject.transform.localPosition.y, Is.EqualTo(0.1f));
            Assert.That(manager.GameplayCamera.nearClipPlane, Is.EqualTo(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(labelObject);
            Object.DestroyImmediate(host);
        }
    }
}
