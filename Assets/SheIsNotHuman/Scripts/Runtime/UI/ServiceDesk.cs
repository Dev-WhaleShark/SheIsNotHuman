using System.Collections;
using DG.Tweening;
using R3;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;
using WhaleShark.Gameplay;
using WhaleShark.Interaction;

namespace WhaleShark.UI
{
    public enum DeskMode { Disabled, Dialogue, Review, Completed }

    /// <summary>One desk's local UI, data and input facade. The game flow subscribes to requests.</summary>
    [DisallowMultipleComponent, MovedFrom(true, "SheIsNotHuman.InspectionMvp", null, "InspectionMvpView")]
    public sealed class ServiceDesk : MonoBehaviour
    {
        [TabGroup("InspectorTabs", "설정"), LabelText("물품 이동 시간 (초)"), MinValue(0)]
        [SerializeField] private float motionSeconds = .35f;
        [TabGroup("InspectorTabs", "설정"), LabelText("전환 애니메이션")]
        [SerializeField] private bool animateTransitions = true;
        [TabGroup("InspectorTabs", "연결"), FoldoutGroup("InspectorTabs/연결/표시", Expanded = false), Required, SerializeField]
        private DialoguePanel dialoguePanel;
        [FoldoutGroup("InspectorTabs/연결/표시"), Required, SerializeField, FormerlySerializedAs("identityDocument")]
        private IdentityCard identityCard;
        [FoldoutGroup("InspectorTabs/연결/표시"), Required, SerializeField, FormerlySerializedAs("orderDocument")]
        private MobileDevice mobileDevice;
        [FoldoutGroup("InspectorTabs/연결/표시"), Required, SerializeField] private RectTransform documentsRoot;
        [FoldoutGroup("InspectorTabs/연결/표시"), Required, SerializeField] private CanvasGroup documentsGroup;
        [FoldoutGroup("InspectorTabs/연결/표시"), Required, SerializeField] private DeskFocus focus;
        [FoldoutGroup("InspectorTabs/연결/표시"), Required, SerializeField] private DeskInteractionContext context;
        [FoldoutGroup("InspectorTabs/연결/표시"), SerializeField, FormerlySerializedAs("deskItems")] private DeskItem[] items;
        [FoldoutGroup("InspectorTabs/연결/표시"), SerializeField] private TMP_Text hint, stateLabel, resultStamp;
        [FoldoutGroup("InspectorTabs/연결/표시"), SerializeField] private UnityEngine.UI.Button restartButton;

        [ShowInInspector, ReadOnly, PropertyOrder(-20), LabelText("책상 모드")]
        public DeskMode Mode { get; private set; } = DeskMode.Disabled;
        [ShowInInspector, ReadOnly, PropertyOrder(-19), LabelText("입력 잠금 사유")]
        public string InputLockReason => inputLock.Reason;
        [ShowInInspector, ReadOnly, PropertyOrder(-18), LabelText("포커스")]
        public GameObject FocusTarget => focus == null ? null : focus.IsDocumentFocus ? focus.FocusedIdentity : focus.FocusedDummy;
        public bool IsAvailable => isActiveAndEnabled;
        public bool IsModalOpen => focus != null && focus.IsOpen && focus.IsDocumentFocus;
        public bool IsModalBusy => focus != null && (focus.IsBusy || (focus.IsOpen && !focus.IsDocumentFocus));
        public bool IsBusy => inputLock.IsLocked || externalBusy || (focus != null && focus.IsBusy);
        public bool BlocksCameraInput => IsBusy || (focus != null && focus.IsOpen) ||
            (context != null && context.HasPointerCapture) || Mode == DeskMode.Disabled;
        public Observable<VisitorDecision> DecisionRequested => decisions;
        public Observable<Unit> RestartRequested => restarts;
        public Observable<bool> AvailabilityChanged => availability;

        private readonly Subject<VisitorDecision> decisions = new();
        private readonly Subject<Unit> restarts = new();
        private readonly Subject<bool> availability = new();
        private readonly InputLock inputLock = new();
        private readonly TweenScope tweens = new();
        private CompositeDisposable subscriptions;
        private Vector2 documentsOrigin;
        private bool externalAllowed = true, externalBusy, captured;
        private int activityVersion;

        private void Awake() => CaptureOrigins();
        private void CaptureOrigins()
        {
            if (captured || documentsRoot == null) return;
            documentsOrigin = documentsRoot.anchoredPosition;
            captured = true;
        }
        private void OnEnable()
        {
            CaptureOrigins();
            subscriptions?.Dispose();
            subscriptions = new CompositeDisposable();
            if (focus != null)
            {
                focus.CloseRequested.Subscribe(_ => RequestClose()).AddTo(subscriptions);
                focus.DecisionRequested.Subscribe(RequestDecision).AddTo(subscriptions);
            }
            if (restartButton != null) restartButton.onClick.AddListener(RequestRestart);
            if (items != null) foreach (var item in items) if (item != null)
            {
                item.CanInteract = CanInteractWith;
                item.FocusRequested = ExpandItem;
            }
            availability.OnNext(true);
            RefreshInput();
        }
        private void OnDisable()
        {
            CancelActivity();
            subscriptions?.Dispose(); subscriptions = null;
            if (restartButton != null) restartButton.onClick.RemoveListener(RequestRestart);
            if (items != null) foreach (var item in items) if (item != null)
            { item.CanInteract = null; item.FocusRequested = null; }
            availability.OnNext(false);
        }
        private void OnDestroy()
        {
            CancelActivity();
            decisions.Dispose(); restarts.Dispose(); availability.Dispose();
        }
        private void Update() => RefreshInput();

        public void Bind(IdentityCardData identity, OrderDisplayData order)
        {
            CancelActivity(); CaptureOrigins();
            identityCard?.Bind(identity); mobileDevice?.Bind(order);
            RestoreItems(true);
            if (documentsRoot != null) documentsRoot.anchoredPosition = documentsOrigin;
            if (documentsGroup != null) documentsGroup.alpha = 0;
            if (resultStamp != null) { resultStamp.text = string.Empty; resultStamp.gameObject.SetActive(false); }
        }
        public void Clear()
        {
            CancelActivity(); identityCard?.Clear(); mobileDevice?.Clear();
            if (documentsGroup != null) documentsGroup.alpha = 0;
            if (resultStamp != null) { resultStamp.text = string.Empty; resultStamp.gameObject.SetActive(false); }
        }
        public void ResetPresentation()
        {
            Clear(); CaptureOrigins(); RestoreItems(false);
            if (documentsRoot != null) documentsRoot.anchoredPosition = documentsOrigin;
            dialoguePanel?.Clear();
            if (restartButton != null) restartButton.gameObject.SetActive(false);
            SetMode(DeskMode.Disabled);
        }
        public void SetMode(DeskMode mode)
        {
            if (Mode != mode)
            {
                context?.Cancel();
                if (focus != null && focus.IsOpen && !focus.IsDocumentFocus) focus.CancelAndRestore();
            }
            Mode = mode; RefreshInput();
        }
        public void SetHint(string text) { if (hint != null) hint.text = text ?? string.Empty; }
        public void SetProgress(int resolved)
        { if (stateLabel != null) stateLabel.text = Mode == DeskMode.Completed ? "프로토타입 완료" : "검수 창구  /  " + resolved + "명 처리"; }
        public void SetExternalInput(bool allowed, bool busy)
        { externalAllowed = allowed; externalBusy = busy; RefreshInput(); }

        public bool CanInteractWith(DeskItem item) => item != null && isActiveAndEnabled && externalAllowed && !IsBusy
            && focus != null && !focus.IsOpen && (context == null || context.ActiveItem == null || context.ActiveItem == item)
            && (item.Kind == DeskItemKind.Document ? Mode == DeskMode.Review : Mode != DeskMode.Disabled);
        public void ExpandItem(DeskItem item)
        {
            if (!CanInteractWith(item)) return;
            context?.Cancel();
            StartCoroutine(item.Kind == DeskItemKind.Document ? SetFocusOpen(true, animateTransitions) : focus.OpenDummy(item, animateTransitions));
        }
        private void RequestClose()
        {
            if (!CanFocusInput || focus == null || !focus.IsOpen) return;
            StartCoroutine(SetFocusOpen(false, animateTransitions));
        }
        private bool CanFocusInput => isActiveAndEnabled && externalAllowed && !IsBusy;
        private void RequestDecision(VisitorDecision decision)
        {
            if (CanFocusInput && Mode == DeskMode.Review && IsModalOpen)
                decisions.OnNext(decision);
        }
        private void RequestRestart()
        { if (CanFocusInput && (focus == null || !focus.IsOpen)) restarts.OnNext(Unit.Default); }
        public IEnumerator SetFocusOpen(bool open, bool animate)
        {
            if (focus == null) yield break;
            if (open && (!CanFocusInput || Mode != DeskMode.Review || focus.IsOpen)) yield break;
            context?.Cancel();
            if (open) yield return focus.OpenDocuments(animate);
            else yield return focus.Close(animate);
            SetHint(open ? "두 문서의 고객 코드를 비교하세요" : "물품을 클릭하여 검사");
            RefreshInput();
        }
        public IEnumerator HandoffItems(bool animate)
        {
            if (documentsRoot == null || documentsGroup == null) yield break;
            int version = activityVersion;
            using (inputLock.Acquire("서류 전달"))
            {
                documentsGroup.alpha = animate ? 0 : 1;
                documentsRoot.anchoredPosition = animate ? documentsOrigin + Vector2.up * 220 : documentsOrigin;
                if (animate) yield return tweens.Play(DOTween.Sequence()
                    .Join(documentsRoot.DOAnchorPos(documentsOrigin, motionSeconds))
                    .Join(documentsGroup.DOFade(1, motionSeconds)), gameObject);
                if (version != activityVersion) yield break;
                documentsGroup.alpha = 1; documentsRoot.anchoredPosition = documentsOrigin;
            }
            RefreshInput();
        }
        public IEnumerator ResolveDocuments(VisitorDecision decision, bool animate)
        {
            int version = activityVersion;
            using (inputLock.Acquire("판정 처리"))
            {
                if (focus != null) yield return focus.Close(animate);
                if (version != activityVersion) yield break;
                if (animate && documentsRoot != null && documentsGroup != null)
                    yield return tweens.Play(DOTween.Sequence()
                        .Join(documentsRoot.DOAnchorPos(documentsOrigin + Vector2.up * 220, motionSeconds))
                        .Join(documentsGroup.DOFade(0, motionSeconds)), gameObject);
                if (version != activityVersion) yield break;
                if (documentsGroup != null) documentsGroup.alpha = 0;
                if (documentsRoot != null) documentsRoot.anchoredPosition = documentsOrigin;
                if (resultStamp != null)
                {
                    resultStamp.text = decision == VisitorDecision.Pass ? "PASS" : "NON PASS";
                    resultStamp.color = decision == VisitorDecision.Pass ? new Color(.1f,.55f,.3f) : new Color(.75f,.18f,.12f);
                    resultStamp.gameObject.SetActive(true);
                    resultStamp.rectTransform.localScale = Vector3.one * (animate ? 1.3f : 1);
                    if (animate) yield return tweens.Play(resultStamp.rectTransform.DOScale(1, .2f), gameObject);
                }
            }
            RefreshInput();
        }
        public void ShowCompleted()
        {
            CancelActivity(); SetMode(DeskMode.Completed);
            dialoguePanel?.Show("프로토타입 완료"); dialoguePanel?.CompleteTyping();
            SetHint("다시 시작하여 같은 순서로 재검수할 수 있습니다.");
            if (restartButton != null) restartButton.gameObject.SetActive(true);
            RefreshInput();
        }
        public void CancelActivity()
        {
            activityVersion++; StopAllCoroutines(); tweens.Cancel(); inputLock.Clear();
            context?.Cancel(); focus?.CancelAndRestore(); dialoguePanel?.Cancel(); mobileDevice?.SetInputEnabled(false);
        }
        private void RestoreItems(bool documentsOnly)
        { if (items != null) foreach (var item in items) if (item != null && (!documentsOnly || item.Kind == DeskItemKind.Document)) item.RestorePosition(); }
        private void RefreshInput()
        {
            bool stable = isActiveAndEnabled && externalAllowed && !IsBusy && (context == null || !context.HasPointerCapture);
            bool open = focus != null && focus.IsOpen;
            dialoguePanel?.SetInputEnabled(stable && !open && Mode == DeskMode.Dialogue);
            focus?.SetInputEnabled(stable && (Mode == DeskMode.Review || (open && !focus.IsDocumentFocus)));
            mobileDevice?.SetInputEnabled(stable && IsModalOpen && Mode == DeskMode.Review);
            if (documentsGroup != null) documentsGroup.interactable = documentsGroup.blocksRaycasts = stable && !open && Mode == DeskMode.Review;
            if (restartButton != null) restartButton.interactable = stable && !open;
        }
        [TabGroup("InspectorTabs", "개발 도구"), Button("문서 확대"), DisableInEditorMode]
        private void DebugOpen() { if (CanFocusInput && Mode == DeskMode.Review) StartCoroutine(SetFocusOpen(true, animateTransitions)); }
        [TabGroup("InspectorTabs", "개발 도구"), Button("확대 닫기"), DisableInEditorMode]
        private void DebugClose() => RequestClose();
    }
}
