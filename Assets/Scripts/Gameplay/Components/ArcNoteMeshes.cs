using System.Collections.Generic;
using Affdata.Core.Geometry;
using UnityEngine;

namespace Arcade.Gameplay
{
    // This scene owns the meshes; definitions are shared with Play through Affdata.Core.
    internal static class ArcNoteMeshes
    {
        private static Mesh segment, shadow, head, cube, sfx, bracket, connection, line;
        private static readonly Dictionary<Sprite, Mesh> sprites = new Dictionary<Sprite, Mesh>();
        public static Mesh Segment => segment ? segment : segment = NoteMeshDefinitions.CreateArcSegment();
        public static Mesh Shadow => shadow ? shadow : shadow = NoteMeshDefinitions.CreateArcShadow();
        public static Mesh Head => head ? head : head = NoteMeshDefinitions.CreateArcHead();
        public static Mesh Cube => cube ? cube : cube = NoteMeshDefinitions.CreateArcTap();
        public static Mesh Sfx => sfx ? sfx : sfx = NoteMeshDefinitions.CreateSfxArcTap();
        public static Mesh Bracket => bracket ? bracket : bracket = NoteMeshDefinitions.CreateSlideBracket();
        public static Mesh Connection => connection ? connection : connection = NoteMeshDefinitions.CreateConnectionLine();

        public static Mesh Line
        {
            get
            {
                if (line) return line;
                line = new Mesh { name = "Instanced guide line" };
                line.vertices = new[] { new Vector3(-.5f,0,0), new Vector3(.5f,0,0),
                    new Vector3(-.5f,0,1), new Vector3(.5f,0,1) };
                line.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
                line.triangles = new[] { 0,2,1,1,2,3 };
                line.RecalculateBounds();
                return line;
            }
        }

        public static Mesh Sprite(Sprite sprite)
        {
            if (sprites.TryGetValue(sprite, out var mesh)) return mesh;
            mesh = NoteMeshDefinitions.CreateSprite(sprite);
            sprites.Add(sprite, mesh);
            return mesh;
        }

        public static void Release()
        {
            foreach (var mesh in new[] { segment, shadow, head, cube, sfx, bracket, connection, line })
                if (mesh) Object.Destroy(mesh);
            foreach (var mesh in sprites.Values) if (mesh) Object.Destroy(mesh);
            sprites.Clear();
        }
    }
}
