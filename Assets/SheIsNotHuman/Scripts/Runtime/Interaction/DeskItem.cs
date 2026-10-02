using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting.APIUpdating;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WhaleShark.Interaction
{
    public enum DeskItemKind { Document, Dummy }
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform)), MovedFrom(true, "SheIsNotHuman.InspectionMvp", null, "DeskInspectableItem")]
    public sealed class DeskItem : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [TabGroup("InspectorTabs", "연결"), FoldoutGroup("InspectorTabs/연결/참조", Expanded = false), Required, SerializeField] private DeskInteractionContext context;
        [FoldoutGroup("InspectorTabs/연결/참조"), Required, SerializeField] private RectTransform deskBounds;
        [TabGroup("InspectorTabs", "설정"), SerializeField] private DeskItemKind kind;
        [TabGroup("InspectorTabs", "설정"), SerializeField] private string displayName = "ITEM";
        [TabGroup("InspectorTabs", "설정"), SerializeField] private Color displayColor = Color.white;
        [TabGroup("InspectorTabs", "설정"), LabelText("드래그 대기 (초)"), MinValue(0), SerializeField] private float holdSeconds = .18f;
        [TabGroup("InspectorTabs", "설정"), LabelText("드래그 거리 (픽셀)"), MinValue(0), SerializeField] private float movePixels = 8f;
        [ShowInInspector, ReadOnly, PropertyOrder(-20), LabelText("드래그 중")] public bool IsDragging { get; private set; }
        public DeskItemKind Kind => kind;
        public string DisplayName => displayName;
        public Color DisplayColor => displayColor;
        public Func<DeskItem, bool> CanInteract { get; set; }
        public Action<DeskItem> FocusRequested { get; set; }
        private RectTransform rect;
        private Vector2 origin, pressScreen, pointerScreen, grabOffset;
        private Camera eventCamera;
        private float pressTime;
        private int pointerId;
        private bool hasOrigin, moved, pendingClick;
        private readonly Vector3[] corners = new Vector3[4];
        private bool Captured => context != null && context.ActiveItem == this;
        private bool Allowed => isActiveAndEnabled && CanInteract != null && CanInteract(this);
        private void Awake() => CaptureOrigin();
        private void CaptureOrigin()
        { if (rect == null) rect = (RectTransform)transform; if (!hasOrigin) { origin = rect.anchoredPosition; hasOrigin = true; } }
        public void Configure(DeskInteractionContext owner, RectTransform bounds, DeskItemKind itemKind)
        { CancelInteraction(); context = owner; deskBounds = bounds; kind = itemKind; CaptureOrigin(); }
        public void OnPointerDown(PointerEventData data)
        {
            pendingClick = false;
            if (data.button != PointerEventData.InputButton.Left || context == null || context.HasPointerCapture || !Allowed || deskBounds == null) return;
            CaptureOrigin(); eventCamera = data.pressEventCamera;
            if (!context.ScreenToLocalPoint(deskBounds, data.position, eventCamera, out var point)) return;
            if (!context.TryCapture(this, data.pointerId)) return;
            grabOffset = (Vector2)deskBounds.InverseTransformPoint(rect.position) - point;
            pointerId = data.pointerId; pressScreen = pointerScreen = data.position;
            pressTime = Time.unscaledTime; moved = IsDragging = false;
        }
        private void Update()
        {
            if (!Captured) return;
            if (!Allowed) { CancelInteraction(); return; }
            // Non-mouse pointers stay event-driven; polling does not release unrelated touches.
#if ENABLE_INPUT_SYSTEM
            if (pointerId < 0 && Mouse.current != null)
            {
                pointerScreen = Mouse.current.position.ReadValue();
                if (!Mouse.current.leftButton.isPressed) { Release(pointerScreen); return; }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (pointerId < 0)
            {
                pointerScreen = Input.mousePosition;
                if (!Input.GetMouseButton(0)) { Release(pointerScreen); return; }
            }
#endif
            Track(pointerScreen);
        }
        private void Track(Vector2 screen)
        {
            pointerScreen = screen;
            moved |= (screen - pressScreen).sqrMagnitude >= movePixels * movePixels;
            if (!IsDragging && moved && Time.unscaledTime - pressTime >= holdSeconds) IsDragging = true;
            if (!IsDragging || deskBounds == null || context == null) return;
            if (!context.ScreenToLocalPoint(deskBounds, screen, eventCamera, out var point)) return;
            rect.position = deskBounds.TransformPoint(point + grabOffset);
            CanvasCoordinateUtility.ClampToBounds(rect, deskBounds, corners);
        }
        private void Release(Vector2 screen)
        {
            if (!Captured) return;
            if (!Allowed) { CancelInteraction(); return; }
            Track(screen); pendingClick = !IsDragging && !moved; context.Release(this); IsDragging = false;
        }
        public void OnPointerUp(PointerEventData data) { if (data.pointerId == pointerId) Release(data.position); }
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || data.pointerId != pointerId || !pendingClick) return;
            pendingClick = false; if (Allowed) FocusRequested?.Invoke(this);
        }
        public void OnInitializePotentialDrag(PointerEventData data) => data.useDragThreshold = true;
        public void OnBeginDrag(PointerEventData data) { if (Captured && data.pointerId == pointerId) Track(data.position); }
        public void OnDrag(PointerEventData data) { if (Captured && data.pointerId == pointerId) Track(data.position); }
        public void OnEndDrag(PointerEventData data) { if (data.pointerId == pointerId) Release(data.position); }
        public void CancelInteraction() { pendingClick = false; IsDragging = false; context?.Release(this); }
        [TabGroup("InspectorTabs", "개발 도구"), Button("기본 위치 복원")]
        public void RestorePosition() { CancelInteraction(); CaptureOrigin(); rect.anchoredPosition = origin; }
        private void OnDisable() => CancelInteraction();
        private void OnDestroy() { CancelInteraction(); CanInteract = null; FocusRequested = null; }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelInteraction(); }
    }
}
