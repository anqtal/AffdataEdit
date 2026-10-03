using System.Collections.Generic;
using UnityEngine;

namespace Arcade.Gameplay
{
    // Geometry is created here, never loaded from a model asset. Shared by drawing and picking.
    internal static class ArcNoteMeshes
    {
        private static Mesh segment, shadow, head, cube, sfx, bracket;
        private static readonly Dictionary<Sprite, Mesh> sprites = new Dictionary<Sprite, Mesh>();
        public static Mesh Segment => segment ? segment : segment = Make("Arc segment",
            new[] { new Vector3(0,.5f,0), new Vector3(0,.5f,1), new Vector3(1,-.5f,1),
                new Vector3(1,-.5f,0), new Vector3(-1,-.5f,1), new Vector3(-1,-.5f,0) },
            new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector2.one, Vector2.right },
            new[] { 0,3,2, 0,2,1, 0,5,4, 0,4,1 });
        public static Mesh Shadow => shadow ? shadow : shadow = Make("Arc shadow",
            new[] { new Vector3(-1,0,0), new Vector3(-1,0,1), new Vector3(1,0,1), new Vector3(1,0,0) },
            new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }, new[] { 0,1,2, 0,2,3 });
        public static Mesh Head => head ? head : head = Make("Arc head",
            new[] { new Vector3(0,.5f,0), new Vector3(1,-.5f,0), new Vector3(0,-.5f,1), new Vector3(-1,-.5f,0) },
            new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.one }, new[] { 0,2,1, 0,3,2 });
        public static Mesh Cube => cube ? cube : cube = CreateCube();
        public static Mesh Sfx => sfx ? sfx : sfx = CreateSfx();
        public static Mesh Bracket => bracket ? bracket : bracket = CreateBracket();

        public static Mesh Sprite(Sprite sprite)
        {
            if (sprites.TryGetValue(sprite, out var result)) return result;
            // Retain the skin's pivot, tight outline and atlas UVs.
            Vector2[] source = sprite.vertices;
            var vertices = new Vector3[source.Length];
            for (int i = 0; i < source.Length; i++) vertices[i] = source[i];
            ushort[] sourceIndices = sprite.triangles;
            var indices = new int[sourceIndices.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = sourceIndices[i];
            result = Make("Note sprite " + sprite.name, vertices, sprite.uv, indices);
            sprites.Add(sprite, result);
            return result;
        }

        private static Mesh Make(string name, Vector3[] vertices, Vector2[] uv, int[] indices)
        {
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = indices };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateCube()
        {
            var b = new Builder();
            // Retain the original cube's face orientation and UV seams.
            var p = new[] { new Vector3(.5f,-.5f,.5f), new Vector3(-.5f,-.5f,.5f),
                new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f), new Vector3(.5f,.5f,-.5f),
                new Vector3(-.5f,.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(-.5f,-.5f,-.5f) };
            b.Face(new[] { p[0],p[2],p[3],p[1] }, new[] { Vector2.zero,Vector2.up,Vector2.one,Vector2.right });
            b.Face(new[] { p[2],p[4],p[5],p[3] }, new[] { Vector2.zero,Vector2.up,Vector2.one,Vector2.right });
            b.Face(new[] { p[4],p[6],p[7],p[5] }, new[] { Vector2.zero,Vector2.up,Vector2.one,Vector2.right });
            b.Face(new[] { p[6],p[0],p[1],p[7] });
            b.Face(new[] { p[1],p[3],p[5],p[7] });
            b.Face(new[] { p[6],p[4],p[2],p[0] });
            return b.Build("ArcTap cube");
        }

        private static Mesh CreateSfx()
        {
            var b = new Builder();
            // Two notched wings and a diamond core; dimensions are in model space.
            for (int side = -1; side <= 1; side += 2)
            {
                var outline = new[] { new Vector2(side * .585f,-.1f), new Vector2(side * .12627f,-.1f),
                    new Vector2(side * .22627f,0), new Vector2(side * .12627f,.1f), new Vector2(side * .585f,.1f) };
                var faceUV = new Vector2[5];
                for (int i = 0; i < 5; i++)
                    faceUV[i] = new Vector2(side < 0 ? (i == 2 ? .2438f : i == 1 || i == 3 ? .3156f : 0)
                        : (i == 2 ? .7563f : i == 1 || i == 3 ? .6844f : 1), outline[i].y * 5 + .5f);
                var backUV = (Vector2[])faceUV.Clone();
                if (side > 0) for (int i = 0; i < backUV.Length; i++) backUV[i].y = 1 - backUV[i].y;
                else faceUV[2].x = .2406f;
                Vector2[][] sides = side < 0 ? new[] {
                    UV(.2469f,0, 0,0, 0,1, .2469f,1), UV(.2031f,0, .2031f,1, 0,1, 0,0),
                    UV(0,0, .2031f,0, .2031f,1, 0,1), UV(.2469f,1, .2469f,0, 0,0, 0,1), UV(.2f,1, .2f,0, 0,0, 0,1)
                } : new[] {
                    UV(1.0031f,0, .7563f,0, .7563f,1, 1.0031f,1), UV(.7969f,1, .7969f,0, 1,0, 1,1),
                    UV(1,0, 1,1, .7969f,1, .7969f,0), UV(.7563f,0, .7563f,1, 1.0031f,1, 1.0031f,0), UV(.8f,0, .8f,1, 1,1, 1,0)
                };
                b.Prism(outline, -.1f, .1f, faceUV, new[] { 0,1,2, 0,2,4, 2,3,4 }, backUV, sides);
            }
            int bodyCount = b.Indices.Count;
            float r = .128f * Mathf.Sqrt(2);
            b.Prism(new[] { new Vector2(r,0), new Vector2(0,-r), new Vector2(-r,0), new Vector2(0,r) },
                -.128f, .128f, UV(0,0, 1,0, 1,1, 0,1), new[] { 0,1,2,0,2,3 }, UV(0,1, 1,1, 1,0, 0,0),
                new[] { UV(1,1, 0,1, 0,0, 1,0), UV(0,0, 1,0, 1,1, 0,1), UV(0,0, 1,0, 1,1, 0,1), UV(0,0, 1,0, 1,1, 0,1) });
            var mesh = b.Build("Sfx ArcTap wings and core");
            mesh.subMeshCount = 2;
            mesh.SetTriangles(b.Indices.GetRange(0, bodyCount), 0);
            mesh.SetTriangles(b.Indices.GetRange(bodyCount, b.Indices.Count - bodyCount), 1);
            return mesh;
        }

        private static Mesh CreateBracket()
        {
            var b = new Builder();
            var outline = new[] { new Vector2(-.00040f,-.03536f), new Vector2(-.03698f,-.03536f),
                new Vector2(-.03698f,.03536f), new Vector2(-.00040f,.03536f), new Vector2(-.00040f,.02121f),
                new Vector2(-.02284f,.02121f), new Vector2(-.02284f,-.02121f), new Vector2(-.00040f,-.02121f) };
            var uv = new Vector2[8];
            for (int i = 0; i < 8; i++) uv[i] = new Vector2(1 + (outline[i].x + .0004f) / .08755f, .5f + outline[i].y / .08755f);
            var backUV = new Vector2[8];
            for (int i = 0; i < 8; i++) backUV[i] = new Vector2(1.0822f-uv[i].x,uv[i].y);
            b.Prism(outline, -.00339f, .00381f, uv, new[] { 0,1,7, 7,1,6, 1,5,6, 2,5,1, 3,5,2, 4,5,3 }, backUV,
                new[] {
                    UV(.0822f,.9038f, .0822f,.986f, .5f,.986f, .5f,.9038f),
                    UV(.0822f,.0962f, 0,.0962f, 0,.9038f, .0822f,.9038f),
                    UV(.5f,.0962f, .5f,.014f, .0822f,.014f, .0822f,.0962f),
                    UV(.5f,.9038f, .5822f,.9038f, .5822f,.7423f, .5f,.7423f),
                    UV(.0822f,.9038f, .0822f,.986f, .3385f,.986f, .3385f,.9038f),
                    UV(.5f,.7423f, .5822f,.7423f, .5822f,.2577f, .5f,.2577f),
                    UV(.3385f,.0962f, .3385f,.014f, .0822f,.014f, .0822f,.0962f),
                    UV(.5f,.2577f, .5822f,.2577f, .5822f,.0962f, .5f,.0962f)
                });
            return b.Build("Slide bracket");
        }

        private static Vector2[] UV(float u0, float v0, float u1, float v1, float u2, float v2, float u3, float v3)
            => new[] { new Vector2(u0,v0),new Vector2(u1,v1),new Vector2(u2,v2),new Vector2(u3,v3) };

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> Indices = new List<int>();
            public void Face(Vector3[] points, Vector2[] coords = null)
            {
                int start = vertices.Count;
                vertices.AddRange(points);
                uv.AddRange(coords ?? new[] { Vector2.zero,Vector2.up,Vector2.one,Vector2.right });
                Indices.AddRange(new[] { start,start+1,start+2, start,start+2,start+3 });
            }
            public void Prism(Vector2[] outline, float back, float front, Vector2[] coords, int[] triangles,
                Vector2[] backCoords = null, Vector2[][] sideCoords = null)
            {
                Vector2 a = outline[triangles[1]] - outline[triangles[0]], c = outline[triangles[2]] - outline[triangles[0]];
                bool clockwise = a.x*c.y - a.y*c.x < 0;
                for (int face = 0; face < 2; face++)
                {
                    int start = vertices.Count;
                    foreach (var p in outline) vertices.Add(new Vector3(p.x, p.y, face == 0 ? front : back));
                    uv.AddRange(face == 0 ? coords : backCoords ?? coords);
                    bool reverse = clockwise ^ (face == 1);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        Indices.Add(start + triangles[i]);
                        Indices.Add(start + triangles[i + (reverse ? 2 : 1)]);
                        Indices.Add(start + triangles[i + (reverse ? 1 : 2)]);
                    }
                }
                for (int i = 0; i < outline.Length; i++)
                {
                    Vector2 p = outline[i], q = outline[(i + 1) % outline.Length];
                    Face(new[] { new Vector3(p.x,p.y,front),new Vector3(p.x,p.y,back),
                        new Vector3(q.x,q.y,back),new Vector3(q.x,q.y,front) }, sideCoords?[i]);
                    if (clockwise)
                        for (int t = Indices.Count - 6; t < Indices.Count; t += 3)
                        { int swap = Indices[t+1]; Indices[t+1] = Indices[t+2]; Indices[t+2] = swap; }
                }
            }
            public Mesh Build(string name) => Make(name, vertices.ToArray(), uv.ToArray(), Indices.ToArray());
        }

        public static void Release()
        {
            foreach (var mesh in new[] { segment, shadow, head, cube, sfx, bracket })
                if (mesh) Object.Destroy(mesh);
            foreach (var mesh in sprites.Values) if (mesh) Object.Destroy(mesh);
            sprites.Clear();
        }
    }
}
