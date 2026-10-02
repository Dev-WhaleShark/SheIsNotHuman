using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using WhaleShark.Gameplay;
using WhaleShark.UI;

namespace WhaleShark.Interaction
{
    /// <summary>Temporarily presents the original instance wrappers; every exit restores their snapshots.</summary>
    [DisallowMultipleComponent]
    public sealed class DeskFocus : MonoBehaviour
    {
        [TabGroup("InspectorTabs", "설정"), LabelText("확대 전환 (초)"), MinValue(0), SerializeField] private float modalSeconds = .2f;
        [TabGroup("InspectorTabs", "설정"), LabelText("카메라 깊이 비율"), Range(.2f, .9f), SerializeField] private float focusDepthRatio = .7f;
        [TabGroup("InspectorTabs", "연결"), FoldoutGroup("InspectorTabs/연결/참조", Expanded = false), Required, SerializeField] private Canvas focusCanvas;
        [FoldoutGroup("InspectorTabs/연결/참조"), Required, SerializeField] private RectTransform itemsRoot, controls;
        [FoldoutGroup("InspectorTabs/연결/참조"), Required, SerializeField] private GameObject shield;
        [FoldoutGroup("InspectorTabs/연결/참조"), Required, SerializeField] private CanvasGroup group;
        [FoldoutGroup("InspectorTabs/연결/참조"), Required, SerializeField] private IdentityCard identityCard;
        [FoldoutGroup("InspectorTabs/연결/참조"), Required, SerializeField] private MobileDevice mobileDevice;
        [FoldoutGroup("InspectorTabs/연결/참조"), Required, SerializeField] private DeskItem identityItem, mobileItem;
        [FoldoutGroup("InspectorTabs/연결/참조"), Required, SerializeField] private Button closeButton, passButton, nonPassButton;
        [ShowInInspector, ReadOnly, PropertyOrder(-20), LabelText("확대 열림")] public bool IsOpen { get; private set; }
        [ShowInInspector, ReadOnly, PropertyOrder(-19), LabelText("전환 중")] public bool IsBusy { get; private set; }
        [ShowInInspector, ReadOnly, PropertyOrder(-18), LabelText("문서 확대")] public bool IsDocumentFocus => IsOpen && !dummyMode;
        [ShowInInspector, ReadOnly, PropertyOrder(-17), LabelText("확대 대상")]
        public GameObject FocusTarget => dummyMode ? FocusedDummy : FocusedIdentity;
        public bool IsFocusActive => focused.Count != 0;
        public GameObject FocusedIdentity => IsFocusActive && !dummyMode && identityItem != null ? identityItem.gameObject : null;
        public GameObject FocusedMobileDevice => IsFocusActive && !dummyMode && mobileItem != null ? mobileItem.gameObject : null;
        public GameObject FocusedDummy => IsFocusActive && dummyMode && dummy != null ? dummy.gameObject : null;
        public Observable<Unit> CloseRequested => closes;
        public Observable<VisitorDecision> DecisionRequested => decisions;
        private readonly Subject<Unit> closes = new();
        private readonly Subject<VisitorDecision> decisions = new();
        private readonly TweenScope tweens = new();
        private readonly List<FocusEntry> focused = new();
        private Camera projectionCamera;
        private DeskItem dummy;
        private bool dummyMode, inputEnabled, identityWasFocused, mobileWasFocused;
        private int version;

        private void OnEnable()
        {
            if (closeButton != null) closeButton.onClick.AddListener(RequestClose);
            if (passButton != null) passButton.onClick.AddListener(RequestPass);
            if (nonPassButton != null) nonPassButton.onClick.AddListener(RequestNonPass);
            RefreshInput();
        }
        private void OnDisable()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(RequestClose);
            if (passButton != null) passButton.onClick.RemoveListener(RequestPass);
            if (nonPassButton != null) nonPassButton.onClick.RemoveListener(RequestNonPass);
            CancelAndRestore();
        }
        private void OnDestroy() { CancelAndRestore(); closes.Dispose(); decisions.Dispose(); }
        public void ConfigureProjection(Camera camera, float depthRatio)
        { projectionCamera = camera; focusDepthRatio = Mathf.Clamp(depthRatio, .2f, .9f); }
        public void SetInputEnabled(bool value) { inputEnabled = value; RefreshInput(); }
        private bool CanInput => isActiveAndEnabled && inputEnabled && IsOpen && !IsBusy;
        private void RequestClose() { if (CanInput) closes.OnNext(Unit.Default); }
        private void RequestPass() { if (CanInput && !dummyMode) decisions.OnNext(VisitorDecision.Pass); }
        private void RequestNonPass() { if (CanInput && !dummyMode) decisions.OnNext(VisitorDecision.NonPass); }
        private void RefreshInput()
        {
            bool allowed = CanInput;
            if (closeButton != null) closeButton.interactable = allowed;
            if (passButton != null) { passButton.gameObject.SetActive(IsOpen && !dummyMode); passButton.interactable = allowed && !dummyMode; }
            if (nonPassButton != null) { nonPassButton.gameObject.SetActive(IsOpen && !dummyMode); nonPassButton.interactable = allowed && !dummyMode; }
            if (group != null) { group.interactable = allowed; group.blocksRaycasts = IsOpen; }
            mobileDevice?.SetInputEnabled(allowed && !dummyMode);
        }
        public IEnumerator OpenDocuments(bool animate)
        {
            if (IsBusy || IsOpen || !isActiveAndEnabled || !HasConnections || identityItem == null || mobileItem == null || identityCard == null || mobileDevice == null) yield break;
            dummyMode = false; dummy = null;
            // Snapshot both before ANY reparenting, including all leaf descendants.
            focused.Add(new FocusEntry((RectTransform)identityItem.transform));
            focused.Add(new FocusEntry((RectTransform)mobileItem.transform));
            identityWasFocused = identityCard.IsFocused; mobileWasFocused = mobileDevice.IsFocused;
            yield return Open(animate);
        }
        public IEnumerator OpenDummy(DeskItem item, bool animate)
        {
            if (IsBusy || IsOpen || !isActiveAndEnabled || !HasConnections || item == null || item.Kind != DeskItemKind.Dummy) yield break;
            dummyMode = true; dummy = item;
            focused.Add(new FocusEntry((RectTransform)item.transform));
            yield return Open(animate);
        }
        private bool HasConnections => focusCanvas != null && itemsRoot != null && controls != null && shield != null && group != null;
        private IEnumerator Open(bool animate)
        {
            int run = ++version;
            IsBusy = IsOpen = true;
            foreach (var entry in focused) entry.Rect.GetComponent<DeskItem>()?.CancelInteraction();
            ConfigureCanvas();
            focusCanvas.gameObject.SetActive(true); shield.SetActive(true); controls.gameObject.SetActive(true);
            group.alpha = 0;
            for (int i = 0; i < focused.Count; i++)
            {
                Vector2 size = dummyMode ? focused[i].Rect.rect.size : i == 0 ? new Vector2(460, 280) : new Vector2(310, 480);
                Vector2 position = dummyMode ? new Vector2(0, 20) : i == 0 ? new Vector2(-230, 30) : new Vector2(235, 45);
                float scale = dummyMode ? Mathf.Min(720f / Mathf.Max(1, size.x), 330f / Mathf.Max(1, size.y)) : 1;
                focused[i].Prepare(itemsRoot, position, size, scale);
            }
            if (!dummyMode) { identityCard.SetFocused(true); mobileDevice.SetFocused(true); }
            controls.SetAsLastSibling(); RefreshInput();
            yield return Animate(true, animate);
            if (run != version || !isActiveAndEnabled) yield break;
            group.alpha = 1; IsBusy = false; RefreshInput();
        }
        public IEnumerator Close(bool animate)
        {
            if (IsBusy || !IsOpen) yield break;
            int run = ++version; IsBusy = true; RefreshInput();
            yield return Animate(false, animate);
            if (run != version) yield break;
            Restore(); IsOpen = IsBusy = false; RefreshInput();
        }
        private IEnumerator Animate(bool opening, bool animate)
        {
            if (!animate)
            {
                group.alpha = opening ? 1 : 0;
                foreach (var entry in focused) entry.Apply(opening);
                yield break;
            }
            float duration = modalSeconds;
            var sequence = DOTween.Sequence().Join(group.DOFade(opening ? 1 : 0, duration));
            foreach (var entry in focused) entry.Animate(sequence, opening, duration);
            yield return tweens.Play(sequence, gameObject);
        }
        private void ConfigureCanvas()
        {
            if (projectionCamera == null) return; // Ordinary overlay/camera canvases retain their authored layout.
            var camera = projectionCamera;
            float depth = float.PositiveInfinity;
            foreach (var entry in focused) depth = Mathf.Min(depth, Vector3.Dot(entry.Rect.position - camera.transform.position, camera.transform.forward));
            depth = Mathf.Max(camera.nearClipPlane + .05f, depth * focusDepthRatio);
            float height = Vector3.Distance(camera.ViewportToWorldPoint(new Vector3(.5f, 0, depth)), camera.ViewportToWorldPoint(new Vector3(.5f, 1, depth)));
            float width = height * camera.aspect;
            float scale = Mathf.Max(.0001f, Mathf.Min(width / 1200f, height / 675f));
            var rect = (RectTransform)focusCanvas.transform;
            rect.SetPositionAndRotation(camera.ViewportToWorldPoint(new Vector3(.5f, .5f, depth)), camera.transform.rotation);
            Vector3 parentScale = rect.parent == null ? Vector3.one : rect.parent.lossyScale;
            rect.localScale = new Vector3(FocusWorldToLocalScale(scale, parentScale.x), FocusWorldToLocalScale(scale, parentScale.y), FocusWorldToLocalScale(scale, parentScale.z));
            rect.sizeDelta = new Vector2(width / scale, height / scale);
            focusCanvas.worldCamera = camera;
        }
        private static float FocusWorldToLocalScale(float worldScale, float parentScale)
            => Mathf.Abs(parentScale) < .0001f ? worldScale : worldScale / parentScale;
        [TabGroup("InspectorTabs", "개발 도구"), Button("확대 취소 및 복원")]
        public void CancelAndRestore()
        {
            version++; tweens.Cancel();
            Restore(); IsOpen = IsBusy = false; inputEnabled = false; RefreshInput();
        }
        private void Restore()
        {
            if (focused.Count != 0)
            {
                if (!dummyMode) { identityCard?.SetFocused(identityWasFocused); mobileDevice?.SetFocused(mobileWasFocused); }
                var snapshots = new List<UILayoutSnapshot>();
                foreach (var entry in focused) snapshots.Add(entry.Snapshot);
                UILayoutSnapshot.RestoreAll(snapshots); focused.Clear();
            }
            dummyMode = false; dummy = null;
            if (shield != null) shield.SetActive(false);
            if (controls != null) controls.gameObject.SetActive(false);
            // Do not deactivate this component's own host; it must remain able to open again.
            if (focusCanvas != null && focusCanvas.gameObject != gameObject && !transform.IsChildOf(focusCanvas.transform)) focusCanvas.gameObject.SetActive(false);
            if (group != null) group.alpha = 0;
        }
        private sealed class FocusEntry
        {
            public readonly UILayoutSnapshot Snapshot;
            public RectTransform Rect => Snapshot.Root;
            private Vector3 destination, destinationScale;
            private Quaternion destinationRotation;
            private Vector2 destinationSize;
            public FocusEntry(RectTransform rect) { Snapshot = new UILayoutSnapshot(rect); }
            public void Prepare(RectTransform parent, Vector2 position, Vector2 size, float scale)
            {
                Rect.SetParent(parent, true); Rect.anchorMin = Rect.anchorMax = Rect.pivot = Vector2.one * .5f;
                Rect.position = Snapshot.WorldPosition;
                destination = parent.TransformPoint(position); destinationRotation = parent.rotation;
                destinationScale = Vector3.one * scale; destinationSize = size;
            }
            public void Animate(Sequence sequence, bool opening, float duration)
            {
                if (Rect == null) return;
                Vector3 parentScale = Rect.parent == null ? Vector3.one : Rect.parent.lossyScale;
                Vector3 returnScale = new(SafeRatio(Snapshot.WorldScale.x, parentScale.x), SafeRatio(Snapshot.WorldScale.y, parentScale.y), SafeRatio(Snapshot.WorldScale.z, parentScale.z));
                sequence.Join(Rect.DOMove(opening ? destination : Snapshot.WorldPosition, duration));
                sequence.Join(Rect.DORotateQuaternion(opening ? destinationRotation : Snapshot.WorldRotation, duration));
                sequence.Join(Rect.DOScale(opening ? destinationScale : returnScale, duration));
                sequence.Join(Rect.DOSizeDelta(opening ? destinationSize : Snapshot.Size, duration));
            }
            public void Apply(bool opening)
            {
                if (Rect == null) return;
                Vector3 parentScale = Rect.parent == null ? Vector3.one : Rect.parent.lossyScale;
                Rect.position = opening ? destination : Snapshot.WorldPosition;
                Rect.rotation = opening ? destinationRotation : Snapshot.WorldRotation;
                Rect.localScale = opening ? destinationScale : new Vector3(SafeRatio(Snapshot.WorldScale.x, parentScale.x), SafeRatio(Snapshot.WorldScale.y, parentScale.y), SafeRatio(Snapshot.WorldScale.z, parentScale.z));
                Rect.sizeDelta = opening ? destinationSize : Snapshot.Size;
            }
            private static float SafeRatio(float value, float divisor) => Mathf.Abs(divisor) < .0001f ? 1 : value / divisor;
        }
    }
}
