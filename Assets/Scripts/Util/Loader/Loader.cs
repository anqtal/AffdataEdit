
using System;
using System.IO;
using UnityEngine;
using Dummiesman;

namespace Arcade.Util.Loader
{
	public static class Loader
	{

        public static Arcade.Audio.BassClip LoadAudioFile(string path) => Arcade.Audio.BassClip.Load(path);

		public static Texture2D LoadTexture2D(string path)
		{
			byte[] file;
			try
			{
				file = File.ReadAllBytes(path);
			}
			catch
			{
				return null;
			}
			//TODO: Completly remove mipmap after GPU optimize
			Texture2D texture = new Texture2D(1, 1);
			bool success = ImageConversion.LoadImage(texture, file, true);
			if (success)
			{
				texture.wrapMode = TextureWrapMode.Clamp;
				texture.name = path;
				texture.mipMapBias = -4;
				return texture;
			}
			else
			{
				UnityEngine.Object.Destroy(texture);
				return null;
			}
		}

		public static Mesh LoadObjMesh(string path)
		{
			try
			{
				OBJLoader loader = new OBJLoader
				{
					SplitMode = SplitMode.None
				};
				GameObject obj = loader.Load(path);
				MeshRenderer renderer = obj.GetComponentInChildren<MeshRenderer>();
				MeshFilter filter = obj.GetComponentInChildren<MeshFilter>();
				Mesh mesh = filter.sharedMesh;
				foreach (var material in renderer.sharedMaterials)
				{
					UnityEngine.Object.Destroy(material);
				}
				UnityEngine.Object.Destroy(obj);
				return mesh;
			}
			catch (System.Exception ex)
			{
				Debug.LogWarning($"Can not load obj mesh, path: {path} ex:{ex}");
			}
			return null;
		}
	}
}