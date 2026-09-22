using SheIsNotHuman.CubeScreen;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SheIsNotHuman.InspectionMvp
{
    /// <summary>문서는 검사 흐름을, 소품은 판정 없는 확대 흐름을 사용한다.</summary>
    public enum DeskItemKind { Document, Dummy }

    /// <summary>책상 물품의 클릭과 드래그를 구분하며 모든 물품이 하나의 포인터 소유권을 공유한다.</summary>
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
        // 개별 물품을 누르는 동안 다른 물품과 화면 회전이 같은 입력을 가져가지 못하게 한다.
        public static DeskInspectableItem ActiveItem { get; private set; }
        public static bool AnyPointerInteraction => ActiveItem != null;

        private RectTransform rect;
        private Vector2 origin, pressScreen, pointerScreen, grabOffset;
        private Camera eventCamera;
        private float pressTime;
        private int pointerId;
        private bool hasOrigin, moved, pendingClick;
        private readonly Vector3[] corners = new Vector3[4];

        // 도메인 재로드를 생략하는 Play Mode에서도 이전 실행의 정적 소유권을 버린다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOwner() => ActiveItem = null;
        private void Awake() => CaptureOrigin();
        private void CaptureOrigin()
        {
            if (rect == null) rect = (RectTransform)transform;
            if (!hasOrigin) { origin = rect.anchoredPosition; hasOrigin = true; }
        }

        /// <summary>허용된 왼쪽 버튼 입력만 점유하고, 집은 위치의 오프셋을 보존해 드래그 시작 시 튀지 않게 한다.</summary>
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
            // 물품 밖에서도 위치를 추적하되 놓기·클릭 전달은 EventSystem과 연계한다.
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

        // 화면 픽셀 이동량과 누른 시간을 모두 만족해야 드래그한다. 물리 화면 좌표는 이곳에서 보정하지 않고
        // 책상 로컬 좌표로 바꿀 때만 렌즈 왜곡 역변환을 적용하여 이중 보정을 피한다.
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

        // 중심점이 아닌 물품 전체 경계를 제한하고, 책상보다 큰 물품은 해당 축의 중앙에 맞춘다.
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

        // 이동한 입력은 클릭 후보에서 제외한다. 짧게 움직여 드래그가 성립하지 않았어도 확대하지 않는다.
        private void Release(Vector2 screen)
        {
            if (ActiveItem != this) return;
            Track(screen);
            pendingClick = !IsDragging && !moved;
            ActiveItem = null;
            IsDragging = false;
            if (host != null) host.RefreshDeskInput();
        }

        /// <summary>누르기를 시작한 포인터의 해제만 처리한다.</summary>
        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId == pointerId) Release(data.position);
        }
        /// <summary>해제 단계에서 확인한 클릭 후보만 확대 요청으로 전달한다.</summary>
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !pendingClick) return;
            pendingClick = false;
            if (host != null && host.CanInteractWith(this)) host.ExpandItem(this);
        }
        // EventSystem의 흔들림 임계값은 유지하고 Track에서 이 물품의 시간·거리 조건을 별도로 적용한다.
        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = true;
        public void OnBeginDrag(PointerEventData data) { if (ActiveItem == this) Track(data.position); }
        public void OnDrag(PointerEventData data) { if (ActiveItem == this) Track(data.position); }
        public void OnEndDrag(PointerEventData data) { if (ActiveItem == this) Release(data.position); }

        /// <summary>상태 변경·비활성화·포커스 상실 시 클릭 후보와 점유를 해제해 뒤늦은 확대를 방지한다.</summary>
        public void CancelInteraction()
        {
            pendingClick = false;
            IsDragging = false;
            if (ActiveItem != this) return;
            ActiveItem = null;
            if (host != null) host.RefreshDeskInput();
        }
        /// <summary>첫 사용 시 기록한 기본 위치로 되돌린다. 확대 닫기는 별도의 클릭 시점 스냅샷을 사용한다.</summary>
        [Button] public void RestorePosition() { CancelInteraction(); CaptureOrigin(); rect.anchoredPosition = origin; }
        private void OnDisable() => CancelInteraction();
        private void OnDestroy() => CancelInteraction();
        private void OnApplicationFocus(bool focused) { if (!focused) CancelInteraction(); }
    }
}
