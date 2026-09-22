using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SheIsNotHuman.InspectionMvp
{
    /// <summary>방문자 순회와 판정을 소유한다. 뷰는 입력 의도만 전달하며 직접 게임 상태를 진행하지 않는다.</summary>
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
        // 코루틴 시작 전에 잠그므로 같은 프레임의 연속 입력도 중복 전환을 만들지 못한다.
        private bool modalTransition;
        private bool started;
        private bool acceptedDecision;

        /// <summary>씬 빌더나 호출자가 표현 계층과 방문자 목록을 연결한다. 실행 중 교체하면 새 구성으로 재시작한다.</summary>
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

        /// <summary>이전 실행을 취소하고 방문자 순회·판정 횟수·화면을 함께 초기화한다.</summary>
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

            // 목록과 제한 수를 고정해 실행 중 Inspector 편집이 순회 순서를 바꾸지 않게 한다.
            // 배열만 복사하므로 개별 NPC 에셋의 내용은 기존 참조를 공유한다.
            activeRoster = roster == null ? Array.Empty<InspectionNpcData>() : (InspectionNpcData[])roster.Clone();
            activeLimit = Mathf.Max(0, npcLimit);
            presentation.Initialize(this);
            presentation.ResetPresentation();
            SetState(InspectionState.Initializing);
            StartCoroutine(EnterNextNpc());
        }

        /// <summary>타이핑 중이면 현재 문장만 완성하고, 완성된 문장에서만 다음 문장이나 서류 전달로 진행한다.</summary>
        [Button("대사 진행 / 타이핑 완료"), DisableInEditorMode]
        public void AdvanceDialogue()
        {
            if (!CanUsePresentation() || State != InspectionState.Dialogue)
                return;
            if (presentation.IsDialogueTyping)
            {
                presentation.CompleteDialogue();
                return; // 한 번의 클릭으로 문장 완성과 다음 단계 진행이 함께 일어나지 않게 한다.
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

        /// <summary>검사 단계에서만 문서 확대를 요청하며 전환이 끝날 때까지 추가 요청을 잠근다.</summary>
        [Button("검사 열기"), DisableInEditorMode]
        public void OpenInspection()
        {
            if (!CanChangeModal() || presentation.IsModalOpen)
                return;
            modalTransition = true;
            StartCoroutine(ChangeModal(true));
        }

        /// <summary>판정 전 문서 원본을 책상으로 돌려보낸다. 이미 닫혔거나 전환 중이면 무시한다.</summary>
        [Button("검사 닫기"), DisableInEditorMode]
        public void CloseInspection()
        {
            if (!CanChangeModal() || !presentation.IsModalOpen)
                return;
            modalTransition = true;
            StartCoroutine(ChangeModal(false));
        }

        /// <summary>열린 검사 화면에서 유효한 판정을 한 번만 수락하고 기록한 뒤 반응·퇴장을 진행한다.</summary>
        public void Decide(InspectionDecision decision)
        {
            if (!CanChangeModal() || !presentation.IsModalOpen || acceptedDecision || CurrentNpc == null)
                return;
            if (decision != InspectionDecision.Pass && decision != InspectionDecision.NonPass)
                return;

            // 로그 콜백이나 뷰 호출에서 재진입해도 같은 NPC를 두 번 처리하지 않도록 먼저 잠근다.
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

        // 비어 있는 목록 항목은 건너뛰며, 실제 판정 횟수나 목록 끝에 도달하면 완료한다.
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

        // 서류 회수 → 선택한 판정의 반응 타이핑 → 유지 시간 → 퇴장을 모두 기다린 뒤 다음 NPC로 간다.
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

        // 컨트롤러의 전환 잠금과 뷰의 실제 연출 잠금을 함께 검사해 다른 경로의 입력도 차단한다.
        private bool CanChangeModal() => CanUsePresentation()
            && State == InspectionState.Inspecting && !modalTransition && !presentation.IsModalBusy;

        private void SetState(InspectionState state)
        {
            State = state;
            presentation.SetState(state);
        }

        // 표현 코루틴도 이 컴포넌트에서 실행된다. 진행을 먼저 멈춘 뒤 뷰의 트윈·입력 점유를 해제한다.
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
