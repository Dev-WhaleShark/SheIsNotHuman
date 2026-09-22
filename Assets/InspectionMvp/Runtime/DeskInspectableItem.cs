using SheIsNotHuman.CubeScreen;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SheIsNotHuman.InspectionMvp
{
    public enum DeskItemKind { Document, Dummy }

    /// <summary>One pointer owner for all desk items. Physical screen coordinates stay unmodified.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class DeskInspectableItem : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerClickHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Required] public InspectionMvpView host;
        [Required] public RectTransform deskBounds;
        public DeskItemKind kind;
        public string displayName = "ITEM";
        public Color displayColor = Color.white;
        [MinValue(0)] public float holdSeconds = .18f;
        [MinValue(0)] public float movePixels = 8f;
        [ShowInInspector, ReadOnly] public bool IsDragging { get; private set; }
        public static DeskInspectableItem ActiveItem { get; private set; }
        public static bool AnyPointerInteraction => ActiveItem != null;

        private RectTransform rect;
        private Vector2 origin, pressScreen, pointerScreen, grabOffset;
        private Camera eventCamera;
        private float pressTime;
        private int pointerId;
        private bool hasOrigin, moved, pendingClick;
        private readonly Vector3[] corners = new Vector3[4];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOwner() => ActiveItem = null;
        private void Awake() => CaptureOrigin();
        private void CaptureOrigin()
        {
            if (rect == null) rect = (RectTransform)transform;
            if (!hasOrigin) { origin = rect.anchoredPosition; hasOrigin = true; }
        }

        public void OnPointerDown(PointerEventData data)
        {
            pendingClick = false;
            if (data.button != PointerEventData.InputButton.Left || ActiveItem != null ||
                host == null || !host.CanInteractWith(this) || deskBounds == null) return;
            CaptureOrigin();
            eventCamera = data.pressEventCamera;
            if (!LensDistortionCoordinates.ScreenPointToLocalPoint(deskBounds, data.position, eventCamera, out var point)) return;
            grabOffset = (Vector2)deskBounds.InverseTransformPoint(rect.position) - point;
            ActiveItem = this;
            pointerId = data.pointerId;
            pressScreen = pointerScreen = data.position;
            pressTime = Time.unscaledTime;
            moved = IsDragging = false;
            host.RefreshDeskInput();
        }

        private void Update()
        {
            if (ActiveItem != this) return;
            if (host == null || !host.CanInteractWith(this)) { CancelInteraction(); return; }
            // Continue tracking outside the graphic; EventSystem still owns release/click routing.
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                pointerScreen = Mouse.current.position.ReadValue();
                if (!Mouse.current.leftButton.isPressed) { Release(pointerScreen); return; }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            pointerScreen = Input.mousePosition;
            if (!Input.GetMouseButton(0)) { Release(pointerScreen); return; }
#endif
            Track(pointerScreen);
        }

        private void Track(Vector2 screen)
        {
            pointerScreen = screen;
            moved |= (screen - pressScreen).sqrMagnitude >= movePixels * movePixels;
            if (!IsDragging && moved && Time.unscaledTime - pressTime >= holdSeconds)
                IsDragging = true;
            if (!IsDragging) return;
            if (!LensDistortionCoordinates.ScreenPointToLocalPoint(deskBounds, screen, eventCamera, out var point)) return;
            rect.position = deskBounds.TransformPoint(point + grabOffset);
            ClampToDesk();
        }

        private void ClampToDesk()
        {
            rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in corners)
            {
                Vector2 local = deskBounds.InverseTransformPoint(corner);
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            Rect bounds = deskBounds.rect;
            Vector2 correction = Vector2.zero;
            correction.x = max.x - min.x > bounds.width ? bounds.center.x - (max.x + min.x) * .5f
                : min.x < bounds.xMin ? bounds.xMin - min.x : max.x > bounds.xMax ? bounds.xMax - max.x : 0;
            correction.y = max.y - min.y > bounds.height ? bounds.center.y - (max.y + min.y) * .5f
                : min.y < bounds.yMin ? bounds.yMin - min.y : max.y > bounds.yMax ? bounds.yMax - max.y : 0;
            rect.position += deskBounds.TransformVector(correction);
        }

        private void Release(Vector2 screen)
        {
            if (ActiveItem != this) return;
            Track(screen);
            pendingClick = !IsDragging && !moved;
            ActiveItem = null;
            IsDragging = false;
            if (host != null) host.RefreshDeskInput();
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId == pointerId) Release(data.position);
        }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !pendingClick) return;
            pendingClick = false;
            if (host != null && host.CanInteractWith(this)) host.ExpandItem(this);
        }
        // Leave EventSystem's jitter threshold intact. Track independently enforces this item's hold + distance gate.
        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = true;
        public void OnBeginDrag(PointerEventData data) { if (ActiveItem == this) Track(data.position); }
        public void OnDrag(PointerEventData data) { if (ActiveItem == this) Track(data.position); }
        public void OnEndDrag(PointerEventData data) { if (ActiveItem == this) Release(data.position); }

        public void CancelInteraction()
        {
            pendingClick = false;
            IsDragging = false;
            if (ActiveItem != this) return;
            ActiveItem = null;
            if (host != null) host.RefreshDeskInput();
        }
        [Button] public void RestorePosition() { CancelInteraction(); CaptureOrigin(); rect.anchoredPosition = origin; }
        private void OnDisable() => CancelInteraction();
        private void OnDestroy() => CancelInteraction();
        private void OnApplicationFocus(bool focused) { if (!focused) CancelInteraction(); }
    }
}
