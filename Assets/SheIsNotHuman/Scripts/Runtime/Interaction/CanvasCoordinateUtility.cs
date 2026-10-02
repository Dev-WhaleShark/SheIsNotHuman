using UnityEngine;

namespace WhaleShark.Interaction
{
    public static class CanvasCoordinateUtility
    {
        public static bool ScreenPointToLocalPoint(RectTransform rect, Vector2 screen, Camera camera, out Vector2 point)
            => RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, camera, out point);

        public static void ClampToBounds(RectTransform item, RectTransform bounds, Vector3[] corners)
        {
            item.GetWorldCorners(corners);
            Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in corners)
            {
                Vector2 local = bounds.InverseTransformPoint(corner);
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            Rect area = bounds.rect;
            Vector2 offset = Vector2.zero;
            offset.x = max.x - min.x > area.width ? area.center.x - (max.x + min.x) * .5f
                : min.x < area.xMin ? area.xMin - min.x : max.x > area.xMax ? area.xMax - max.x : 0;
            offset.y = max.y - min.y > area.height ? area.center.y - (max.y + min.y) * .5f
                : min.y < area.yMin ? area.yMin - min.y : max.y > area.yMax ? area.yMax - max.y : 0;
            item.position += bounds.TransformVector(offset);
        }
    }
}
