using UnityEngine;

namespace Arcade.Gameplay
{
    public sealed partial class ArcNoteRenderer
    {
        internal void SubmitLine(Vector3 start, Vector3 end, float width, Color color,
            string layer, int order, Vector3 normal = default)
        {
            Vector3 direction = end - start;
            float length = direction.magnitude;
            if (length <= .000001f || width <= 0) return;
            direction /= length;
            // Horizontal guides face the camera; wall guides retain their panel's plane.
            if (normal == Vector3.zero)
            {
                Vector3 view = orthographic ? -cameraForward : cameraPosition - (start + end) * .5f;
                normal = Vector3.ProjectOnPlane(view, direction);
            }
            if (normal.sqrMagnitude <= .000000000001f) return;
            var matrix = Matrix4x4.TRS(start, Quaternion.LookRotation(direction, normal), new Vector3(width,1,length));
            var data = NoteInstance.Create(matrix, 0);
            data.HighColor = color;
            Submit(ArcNoteMeshes.Line, null, data, layer, order);
        }

        internal void SubmitSprite(Sprite sprite, Matrix4x4 matrix, Color color, string layer, int order, int queue)
        {
            var data = NoteInstance.Create(matrix, 0);
            data.HighColor = color;
            Submit(ArcNoteMeshes.Sprite(sprite), sprite.texture, data, layer, order, queue);
        }
    }
}
