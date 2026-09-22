using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SheIsNotHuman.InspectionMvp
{
    /// <summary>Owns all progression; views submit intents and never advance state themselves.</summary>
    [DisallowMultipleComponent]
    public sealed class InspectionFlowController : MonoBehaviour
    {
        [SerializeField, Required] private InspectionPresentation presentation;
        [SerializeField, LabelText("방문자 순서")] private InspectionNpcData[] roster;
        [Title("테스트 설정"), LabelText("전환 애니메이션")] public bool animateTransitions = true;
        [LabelText("방문자 수"), MinValue(1)] public int npcLimit = 3;

        [ShowInInspector, ReadOnly] public InspectionState State { get; private set; } = InspectionState.Initializing;
        [ShowInInspector, ReadOnly] public InspectionNpcData CurrentNpc { get; private set; }
        [ShowInInspector, ReadOnly] public int CurrentIndex { get; private set; } = -1;
        [ShowInInspector, ReadOnly] public int ResolvedCount { get; private set; }

        private InspectionNpcData[] activeRoster = Array.Empty<InspectionNpcData>();
        private int activeLimit;
        private int dialogueIndex;
        private bool modalTransition;
        private bool started;
        private bool acceptedDecision;

        public void Configure(InspectionPresentation view, InspectionNpcData[] npcs)
        {
            CancelRun();
            presentation = view;
            roster = npcs;
            if (started && isActiveAndEnabled)
                Restart();
        }

        private void Start()
        {
            started = true;
            Restart();
        }

        private void OnEnable()
        {
            if (started)
                Restart();
        }

        private void OnDisable()
        {
            CancelRun();
            State = InspectionState.Initializing;
        }

        private void OnDestroy() => CancelRun();

        [Button("처음부터 시작"), DisableInEditorMode]
        public void Restart()
        {
            CancelRun();
            CurrentNpc = null;
            CurrentIndex = -1;
            ResolvedCount = 0;
            dialogueIndex = -1;
            acceptedDecision = false;
            State = InspectionState.Initializing;
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;
            if (presentation == null)
            {
                Debug.LogError("[InspectionMvp] Presentation is missing; configure the flow before starting.", this);
                return;
            }

            // Snapshot the roster so Inspector edits cannot change an in-flight loop.
            activeRoster = roster == null ? Array.Empty<InspectionNpcData>() : (InspectionNpcData[])roster.Clone();
            activeLimit = Mathf.Max(0, npcLimit);
            presentation.Initialize(this);
            presentation.ResetPresentation();
            SetState(InspectionState.Initializing);
            StartCoroutine(EnterNextNpc());
        }

        [Button("대사 진행 / 타이핑 완료"), DisableInEditorMode]
        public void AdvanceDialogue()
        {
            if (!CanUsePresentation() || State != InspectionState.Dialogue)
                return;
            if (presentation.IsDialogueTyping)
            {
                presentation.CompleteDialogue();
                return; // A typing completion must never consume another line or hand off items.
            }

            dialogueIndex++;
            if (CurrentNpc.dialogue != null && dialogueIndex < CurrentNpc.dialogue.Length)
                presentation.ShowDialogue(CurrentNpc.dialogue[dialogueIndex] ?? string.Empty);
            else
            {
                SetState(InspectionState.ItemHandoff);
                StartCoroutine(Handoff());
            }
        }

        [Button("검사 열기"), DisableInEditorMode]
        public void OpenInspection()
        {
            if (!CanChangeModal() || presentation.IsModalOpen)
                return;
            modalTransition = true;
            StartCoroutine(ChangeModal(true));
        }

        [Button("검사 닫기"), DisableInEditorMode]
        public void CloseInspection()
        {
            if (!CanChangeModal() || !presentation.IsModalOpen)
                return;
            modalTransition = true;
            StartCoroutine(ChangeModal(false));
        }

        public void Decide(InspectionDecision decision)
        {
            if (!CanChangeModal() || !presentation.IsModalOpen || acceptedDecision || CurrentNpc == null)
                return;
            if (decision != InspectionDecision.Pass && decision != InspectionDecision.NonPass)
                return;

            // Lock synchronously, before logging, starting any coroutine, or invoking view callbacks.
            acceptedDecision = true;
            State = InspectionState.Resolving;
            InspectionDecision expected = InspectionRule.ExpectedDecision(CurrentNpc);
            ResolvedCount++;
            var record = new DecisionRecord
            {
                npcId = CurrentNpc.npcId,
                idCustomerCode = CurrentNpc.identity?.customerCode,
                orderCustomerCode = CurrentNpc.order?.customerCode,
                playerDecision = DecisionLabel(decision),
                expectedDecision = DecisionLabel(expected),
                isCorrect = decision == expected
            };
            Debug.Log("[InspectionMvp] " + JsonUtility.ToJson(record), this);
            presentation.SetState(State);
            StartCoroutine(ResolveAndLeave(decision));
        }

        [ButtonGroup("판정"), Button("PASS"), DisableInEditorMode]
        private void DebugPass() => Decide(InspectionDecision.Pass);

        [ButtonGroup("판정"), Button("NON PASS"), DisableInEditorMode]
        private void DebugNonPass() => Decide(InspectionDecision.NonPass);

        private IEnumerator EnterNextNpc()
        {
            CurrentNpc = null;
            if (ResolvedCount < activeLimit)
            {
                while (++CurrentIndex < activeRoster.Length)
                {
                    if (activeRoster[CurrentIndex] == null)
                        continue;
                    CurrentNpc = activeRoster[CurrentIndex];
                    break;
                }
            }

            if (CurrentNpc == null)
            {
                SetState(InspectionState.Completed);
                presentation.ShowCompleted();
                yield break;
            }

            acceptedDecision = false;
            dialogueIndex = 0;
            presentation.BindNpc(CurrentNpc);
            SetState(InspectionState.NpcEntering);
            yield return presentation.EnterNpc(animateTransitions);
            if (CurrentNpc.dialogue == null || CurrentNpc.dialogue.Length == 0)
            {
                SetState(InspectionState.ItemHandoff);
                yield return Handoff();
                yield break;
            }

            SetState(InspectionState.Dialogue);
            presentation.SetHint("대사창을 클릭하여 계속");
            presentation.ShowDialogue(CurrentNpc.dialogue[0] ?? string.Empty);
        }

        private IEnumerator Handoff()
        {
            presentation.SetHint("서류를 받는 중입니다.");
            yield return presentation.HandoffItems(animateTransitions);
            SetState(InspectionState.Inspecting);
            presentation.SetHint("물품을 클릭하여 검사");
        }

        private IEnumerator ChangeModal(bool open)
        {
            yield return presentation.SetInspectionOpen(open, animateTransitions);
            modalTransition = false;
            presentation.SetHint(open ? "두 문서의 고객 코드를 비교하세요" : "물품을 클릭하여 검사");
        }

        private IEnumerator ResolveAndLeave(InspectionDecision decision)
        {
            presentation.SetHint("판정을 처리하는 중입니다.");
            yield return presentation.ResolveDocuments(decision, animateTransitions);
            if (presentation.IsModalOpen)
                yield return presentation.SetInspectionOpen(false, animateTransitions);

            string reaction = decision == InspectionDecision.Pass ? CurrentNpc.passReaction : CurrentNpc.nonPassReaction;
            presentation.ShowDialogue(reaction ?? string.Empty);
            while (presentation.IsDialogueTyping)
                yield return null;
            float hold = presentation.ReactionHoldSeconds;
            if (hold > 0f)
                yield return new WaitForSecondsRealtime(hold);
            SetState(InspectionState.NpcExiting);
            yield return presentation.ExitNpc(animateTransitions);
            yield return EnterNextNpc();
        }

        private bool CanUsePresentation() => Application.isPlaying && isActiveAndEnabled && presentation != null;

        private bool CanChangeModal() => CanUsePresentation()
            && State == InspectionState.Inspecting && !modalTransition && !presentation.IsModalBusy;

        private void SetState(InspectionState state)
        {
            State = state;
            presentation.SetState(state);
        }

        private void CancelRun()
        {
            StopAllCoroutines();
            modalTransition = false;
            if (presentation != null)
                presentation.CancelActivity();
        }

        private static string DecisionLabel(InspectionDecision value) => value == InspectionDecision.Pass ? "PASS" : "NON PASS";

        [Serializable]
        private sealed class DecisionRecord
        {
            public string npcId;
            public string idCustomerCode;
            public string orderCustomerCode;
            public string playerDecision;
            public string expectedDecision;
            public bool isCorrect;
        }
    }
}
