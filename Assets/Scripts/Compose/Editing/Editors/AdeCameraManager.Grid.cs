using Arcade.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arcade.Compose
{
	public partial class AdeCameraManager
	{
		private GameObject freeCameraGrid;
		private Mesh gridMesh;
		private Material gridMaterial;
		private int gridLevel, gridX, gridZ;
		private readonly Vector3[] gridVertices = new Vector3[4004];
		private static readonly int GridLevelId = Shader.PropertyToID("_Level");

		private void UpdateFreeCameraGrid()
		{
			if (!FreeCameraEnabled)
			{
				if (freeCameraGrid) freeCameraGrid.SetActive(false);
				return;
			}
			if (!freeCameraGrid)
			{
				freeCameraGrid = new GameObject("Free camera world grid", typeof(MeshFilter), typeof(MeshRenderer));
				freeCameraGrid.layer = ArcCameraManager.Instance.SkyInputLabel.gameObject.layer;
				gridMesh = new Mesh { name = "AA free camera grid" };
				gridMesh.MarkDynamic();
				freeCameraGrid.GetComponent<MeshFilter>().sharedMesh = gridMesh;
				gridMaterial = new Material(Resources.Load<Shader>("AffdataEdit/FreeCamera/Grid"));
				var renderer = freeCameraGrid.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = gridMaterial;
				renderer.shadowCastingMode = ShadowCastingMode.Off;
				renderer.receiveShadows = false;
				renderer.renderingLayerMask = 1u & ~ArcGameplayManager.Instance.SelectionLayerMask;
				renderer.sortingLayerName = "Effect";
			}
			freeCameraGrid.SetActive(true);
			var position = ArcCameraManager.Instance.GameplayCamera.transform.position;
			int level = Mathf.Max(1, (int)Mathf.Abs(position.y / 50f));
			int x = (int)(position.x / level) * level, z = (int)(position.z / level) * level;
			gridMaterial.SetFloat(GridLevelId, Mathf.Max(Mathf.Abs(position.y / 10f), .5f));
			if (gridLevel == level && gridX == x && gridZ == z) return;
			gridLevel = level;
			gridX = x;
			gridZ = z;
			// Match GridDrawer.DrawGrid: y=0, 500 lines each side, camera-snapped origin.
			float halfRange = 250f * level;
			int vertex = 0;
			for (int i = -500; i <= 500; i++)
			{
				float offset = i * level;
				gridVertices[vertex++] = new Vector3(offset + x, 0, -halfRange + z);
				gridVertices[vertex++] = new Vector3(offset + x, 0, halfRange + z);
				gridVertices[vertex++] = new Vector3(-halfRange + x, 0, offset + z);
				gridVertices[vertex++] = new Vector3(halfRange + x, 0, offset + z);
			}
			bool firstBuild = gridMesh.vertexCount == 0;
			gridMesh.vertices = gridVertices;
			if (firstBuild)
			{
				var indices = new int[gridVertices.Length];
				for (int i = 0; i < indices.Length; i++) indices[i] = i;
				gridMesh.SetIndices(indices, MeshTopology.Lines, 0);
			}
			gridMesh.RecalculateBounds();
		}

		private void LateUpdate() => UpdateFreeCameraGrid();

		private void OnDestroy()
		{
			if (freeCameraGrid) Destroy(freeCameraGrid);
			if (gridMesh) Destroy(gridMesh);
			if (gridMaterial) Destroy(gridMaterial);
		}
	}
}
