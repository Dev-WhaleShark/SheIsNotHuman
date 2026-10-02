using System;
using System.Collections;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;
using WhaleShark.Rendering;
using WhaleShark.UI;

namespace WhaleShark.Gameplay
{
    /// <summary>Owns visitor progression and accepted decisions; UI emits local requests only.</summary>
    [DisallowMultipleComponent, MovedFrom(true, "SheIsNotHuman.InspectionMvp", null, "InspectionFlowController")]
    public sealed class GameFlowManager : MonoBehaviour
    {
        [TabGroup("InspectorTabs", "연결"), FoldoutGroup("InspectorTabs/연결/참조", Expanded = false), SerializeField, Required]
        private ServiceDesk desk;
        [FoldoutGroup("InspectorTabs/연결/참조"), SerializeField, Required] private DialoguePlayer dialogue;
        [FoldoutGroup("InspectorTabs/연결/참조"), SerializeField, Required] private VisitorActor visitor;
        [FoldoutGroup("InspectorTabs/연결/참조"), SerializeField, LabelText("큐브 연결 (선택)")] private CubeDeskAdapter cubeAdapter;
        [TabGroup("InspectorTabs", "설정"), SerializeField, LabelText("방문자 순서")] private VisitorProfile[] roster;
        [TabGroup("InspectorTabs", "설정"), SerializeField, LabelText("전환 애니메이션")] private bool animateTransitions = true;
        [TabGroup("InspectorTabs", "설정"), SerializeField, FormerlySerializedAs("npcLimit"), MinValue(1), LabelText("방문자 수")]
        private int visitorLimit = 3;
        [TabGroup("InspectorTabs", "설정"), SerializeField, MinValue(0), LabelText("반응 유지 시간 (초)")]
        private float reactionHoldSeconds = 1.2f;

        [ShowInInspector, ReadOnly, PropertyOrder(-20), LabelText("진행 단계")]
        public GamePhase Phase { get; private set; } = GamePhase.Initializing;
        [ShowInInspector, ReadOnly, PropertyOrder(-19), LabelText("현재 방문자")]
        public VisitorProfile CurrentVisitor { get; private set; }
        [ShowInInspector, ReadOnly, PropertyOrder(-18), LabelText("방문자 인덱스")]
        public int CurrentIndex { get; private set; } = -1;
        [ShowInInspector, ReadOnly, PropertyOrder(-17), LabelText("처리한 방문자 수")]
        public int ResolvedCount { get; private set; }
        [ShowInInspector, ReadOnly, PropertyOrder(-16), LabelText("대사 진행")]
        private string DialogueProgress => dialogue == null ? "연결 없음" : (dialogue.Index + 1) + " / " + dialogue.Count;

        private VisitorProfile[] activeRoster = Array.Empty<VisitorProfile>();
        private int activeLimit;
        private CompositeDisposable subscriptions;
        private bool started, acceptedDecision, modalTransition;
        private int runVersion;

        public void Configure(ServiceDesk serviceDesk, DialoguePlayer dialoguePlayer, VisitorActor visitorActor,
            VisitorProfile[] visitors, CubeDeskAdapter adapter = null)
        {
            CancelRun();
            DisposeSubscriptions();
            desk = serviceDesk; dialogue = dialoguePlayer; visitor = visitorActor;
            roster = visitors; cubeAdapter = adapter;
            Phase = GamePhase.Initializing;
            if (isActiveAndEnabled) SubscribeRequests();
            if (started && isActiveAndEnabled) Restart();
        }
        private void OnEnable()
        {
            SubscribeRequests();
            if (started) Restart();
        }
        private void Start() { started = true; Restart(); }
        private void OnDisable()
        {
            DisposeSubscriptions(); CancelRun(); Phase = GamePhase.Initializing;
        }
        private void OnDestroy() { DisposeSubscriptions(); CancelRun(); }
        private void DisposeSubscriptions() { subscriptions?.Dispose(); subscriptions = null; }
        private void SubscribeRequests()
        {
            DisposeSubscriptions();
            subscriptions = new CompositeDisposable();
            if (desk == null) return;
            desk.DecisionRequested.Subscribe(Decide).AddTo(subscriptions);
            desk.RestartRequested.Subscribe(_ => Restart()).AddTo(subscriptions);
            desk.AvailabilityChanged.Subscribe(OnAvailabilityChanged).AddTo(subscriptions);
        }
        private void OnAvailabilityChanged(bool available)
        {
            if (!isActiveAndEnabled) return;
            if (!available) { CancelRun(); Phase = GamePhase.Initializing; }
            else if (started && Phase == GamePhase.Initializing) Restart();
        }

        [TabGroup("InspectorTabs", "개발 도구"), Button("처음부터 시작"), DisableInEditorMode]
        public void Restart()
        {
            CancelRun();
            CurrentVisitor = null; CurrentIndex = -1; ResolvedCount = 0;
            acceptedDecision = modalTransition = false;
            Phase = GamePhase.Initializing;
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            if (desk == null || dialogue == null || visitor == null)
            {
                Debug.LogError("[ServiceDesk] GameFlowManager requires desk, dialogue and visitor connections.", this);
                return;
            }
            if (!desk.IsAvailable || !dialogue.isActiveAndEnabled || !visitor.isActiveAndEnabled) return;
            activeRoster = roster == null ? Array.Empty<VisitorProfile>() : (VisitorProfile[])roster.Clone();
            activeLimit = Mathf.Max(0, visitorLimit);
            desk.ResetPresentation(); visitor.ResetActor();
            SetPhase(GamePhase.Initializing);
            StartCoroutine(EnterNextVisitor(runVersion));
        }

        [TabGroup("InspectorTabs", "개발 도구"), Button("대사 진행 / 타이핑 완료"), DisableInEditorMode]
        public void AdvanceDialogue()
        {
            if (CanUseDesk() && Phase == GamePhase.Dialogue && !desk.IsBusy && !desk.IsModalBusy && !desk.IsModalOpen)
                dialogue.Advance();
        }
        [TabGroup("InspectorTabs", "개발 도구"), Button("검사 열기"), DisableInEditorMode]
        public void OpenReview()
        {
            if (!CanChangeModal() || desk.IsModalOpen) return;
            modalTransition = true;
            StartCoroutine(ChangeModal(true, runVersion));
        }
        [TabGroup("InspectorTabs", "개발 도구"), Button("검사 닫기"), DisableInEditorMode]
        public void CloseReview()
        {
            if (!CanChangeModal() || !desk.IsModalOpen) return;
            modalTransition = true;
            StartCoroutine(ChangeModal(false, runVersion));
        }
        private IEnumerator ChangeModal(bool open, int version)
        {
            yield return desk.SetFocusOpen(open, animateTransitions);
            if (!IsCurrentRun(version)) yield break;
            modalTransition = false;
        }
        public void Decide(VisitorDecision decision)
        {
            if (!CanChangeModal() || !desk.IsModalOpen || acceptedDecision || CurrentVisitor == null) return;
            if (decision != VisitorDecision.Pass && decision != VisitorDecision.NonPass) return;
            // Lock before logging or calling external code, including reentrant event listeners.
            acceptedDecision = true;
            Phase = GamePhase.Resolving;
            VisitorDecision expected = CustomerCodeRule.ExpectedDecision(CurrentVisitor);
            ResolvedCount++;
            int version = runVersion;
            var record = new DecisionRecord
            {
                npcId = CurrentVisitor.npcId,
                idCustomerCode = CurrentVisitor.identity?.customerCode,
                orderCustomerCode = CurrentVisitor.order?.customerCode,
                playerDecision = DecisionLabel(decision), expectedDecision = DecisionLabel(expected),
                isCorrect = decision == expected
            };
            Debug.Log("[ServiceDesk] " + JsonUtility.ToJson(record), this);
            if (!IsCurrentRun(version)) return;
            SetPhase(GamePhase.Resolving);
            StartCoroutine(ResolveAndLeave(decision, version));
        }
        [TabGroup("InspectorTabs", "개발 도구"), Button("PASS"), DisableInEditorMode]
        private void DebugPass() => Decide(VisitorDecision.Pass);
        [TabGroup("InspectorTabs", "개발 도구"), Button("NON PASS"), DisableInEditorMode]
        private void DebugNonPass() => Decide(VisitorDecision.NonPass);

        private IEnumerator EnterNextVisitor(int version)
        {
            if (!IsCurrentRun(version)) yield break;
            CurrentVisitor = null;
            if (ResolvedCount < activeLimit)
            {
                while (++CurrentIndex < activeRoster.Length)
                {
                    if (activeRoster[CurrentIndex] == null) continue;
                    CurrentVisitor = activeRoster[CurrentIndex]; break;
                }
            }
            if (CurrentVisitor == null)
            {
                SetPhase(GamePhase.Completed);
                if (cubeAdapter != null) yield return cubeAdapter.FocusBottom(false);
                if (!IsCurrentRun(version)) yield break;
                desk.ShowCompleted(); desk.SetProgress(ResolvedCount);
                yield break;
            }
            acceptedDecision = false;
            // Each leaf receives a copied presentation payload, never the domain asset.
            desk.Bind(CurrentVisitor.identity == null ? null : new IdentityCardData
            {
                displayName = CurrentVisitor.identity.displayName, customerCode = CurrentVisitor.identity.customerCode,
                footer = CurrentVisitor.npcId, portraitColor = CurrentVisitor.portraitColor
            }, CurrentVisitor.order == null ? null : new OrderDisplayData
            {
                customerName = CurrentVisitor.identity?.displayName ?? string.Empty, customerCode = CurrentVisitor.order.customerCode,
                orderNumber = CurrentVisitor.order.orderNumber, productName = CurrentVisitor.order.productName,
                quantity = CurrentVisitor.order.quantity
            });
            visitor.Bind(CurrentVisitor.displayName, CurrentVisitor.npcId, CurrentVisitor.portraitColor);
            SetPhase(GamePhase.VisitorEntering);
            if (cubeAdapter != null) yield return cubeAdapter.FocusFront(animateTransitions);
            if (!IsCurrentRun(version)) yield break;
            yield return visitor.Enter(animateTransitions);
            if (!IsCurrentRun(version)) yield break;
            if (cubeAdapter != null) yield return cubeAdapter.FocusBottom(animateTransitions);
            if (!IsCurrentRun(version)) yield break;
            if (CurrentVisitor.dialogue != null && CurrentVisitor.dialogue.Length > 0)
            {
                SetPhase(GamePhase.Dialogue);
                desk.SetHint("대사창을 클릭하여 계속");
                yield return dialogue.PlayAndWait(CurrentVisitor.dialogue);
                if (!IsCurrentRun(version)) yield break;
            }
            SetPhase(GamePhase.ItemHandoff);
            desk.SetHint("서류를 받는 중입니다.");
            yield return dialogue.Hide(animateTransitions);
            if (!IsCurrentRun(version)) yield break;
            yield return desk.HandoffItems(animateTransitions);
            if (!IsCurrentRun(version)) yield break;
            SetPhase(GamePhase.Review);
            desk.SetHint("물품을 클릭하여 검사");
        }
        private IEnumerator ResolveAndLeave(VisitorDecision decision, int version)
        {
            desk.SetHint("판정을 처리하는 중입니다.");
            // The local facade restores originals before showing a stamp and retrieving documents.
            yield return desk.ResolveDocuments(decision, animateTransitions);
            if (!IsCurrentRun(version)) yield break;
            string reaction = decision == VisitorDecision.Pass ? CurrentVisitor.passReaction : CurrentVisitor.nonPassReaction;
            dialogue.Play(new[] { reaction ?? string.Empty });
            while (dialogue.IsTyping && IsCurrentRun(version)) yield return null;
            if (!IsCurrentRun(version)) yield break;
            if (reactionHoldSeconds > 0) yield return new WaitForSecondsRealtime(reactionHoldSeconds);
            if (!IsCurrentRun(version)) yield break;
            dialogue.Advance();
            if (!IsCurrentRun(version)) yield break;
            SetPhase(GamePhase.VisitorExiting);
            yield return dialogue.Hide(animateTransitions);
            if (!IsCurrentRun(version)) yield break;
            if (cubeAdapter != null) yield return cubeAdapter.FocusFront(animateTransitions);
            if (!IsCurrentRun(version)) yield break;
            yield return visitor.Exit(animateTransitions);
            if (!IsCurrentRun(version)) yield break;
            dialogue.Cancel(); desk.Clear();
            yield return EnterNextVisitor(version);
        }
        private bool CanUseDesk() => Application.isPlaying && isActiveAndEnabled && desk != null && desk.IsAvailable
            && dialogue != null && dialogue.isActiveAndEnabled && visitor != null && visitor.isActiveAndEnabled;
        private bool CanChangeModal() => CanUseDesk() && Phase == GamePhase.Review && !modalTransition && !desk.IsModalBusy && !desk.IsBusy;
        private bool IsCurrentRun(int version) => version == runVersion && CanUseDesk();
        private void SetPhase(GamePhase phase)
        {
            Phase = phase;
            desk.SetMode(phase == GamePhase.Dialogue ? DeskMode.Dialogue : phase == GamePhase.Review ? DeskMode.Review
                : phase == GamePhase.Completed ? DeskMode.Completed : DeskMode.Disabled);
            desk.SetProgress(ResolvedCount);
        }
        private void CancelRun()
        {
            runVersion++; StopAllCoroutines(); modalTransition = false;
            dialogue?.Cancel(); visitor?.Cancel(); desk?.CancelActivity(); cubeAdapter?.ResetConnection();
        }
        private static string DecisionLabel(VisitorDecision decision) => decision == VisitorDecision.Pass ? "PASS" : "NON PASS";
        [Serializable]
        private sealed class DecisionRecord
        {
            public string npcId, idCustomerCode, orderCustomerCode, playerDecision, expectedDecision;
            public bool isCorrect;
        }
    }
}
