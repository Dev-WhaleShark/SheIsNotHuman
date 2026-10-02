using Sirenix.OdinInspector;
using UnityEngine;

namespace WhaleShark.Interaction
{
    public delegate bool ScreenPointToLocalPoint(RectTransform rect, Vector2 screen, Camera camera, out Vector2 point);

    [DisallowMultipleComponent]
    public sealed class DeskInteractionContext : MonoBehaviour
    {
        [ShowInInspector, ReadOnly, PropertyOrder(-20), LabelText("포인터 점유")]
        public bool HasPointerCapture => ActiveItem != null;
        [ShowInInspector, ReadOnly, PropertyOrder(-19), LabelText("점유 물품")]
        public DeskItem ActiveItem { get; private set; }
        [ShowInInspector, ReadOnly, PropertyOrder(-18), LabelText("포인터 ID")]
        public int PointerId { get; private set; }
        public ScreenPointToLocalPoint CoordinateConverter { get; set; }
        public bool TryCapture(DeskItem item, int pointerId)
        {
            if (item == null || !isActiveAndEnabled || HasPointerCapture) return false;
            ActiveItem = item; PointerId = pointerId; return true;
        }
        public void Release(DeskItem item)
        { if (ActiveItem == item) { ActiveItem = null; PointerId = 0; } }
        public bool ScreenToLocalPoint(RectTransform rect, Vector2 screen, Camera camera, out Vector2 point)
        {
            if (CoordinateConverter != null) return CoordinateConverter(rect, screen, camera, out point);
            return CanvasCoordinateUtility.ScreenPointToLocalPoint(rect, screen, camera, out point);
        }
        [TabGroup("InspectorTabs", "개발 도구"), Button("점유 취소")]
        public void Cancel() { var item = ActiveItem; ActiveItem = null; PointerId = 0; item?.CancelInteraction(); }
        private void OnDisable() => Cancel();
        private void OnDestroy() { Cancel(); CoordinateConverter = null; }
    }
}
