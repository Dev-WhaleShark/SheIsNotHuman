using System.Collections;
using UnityEngine;

namespace SheIsNotHuman.InspectionMvp
{
    /// <summary>진행을 소유한 컨트롤러와 화면 표현 사이의 계약이다. 컨트롤러가 연출 완료를 기다린 뒤 상태를 진행한다.</summary>
    public abstract class InspectionPresentation : MonoBehaviour
    {
        // 타이핑 완료 입력과 다음 대사 입력을 구분하고, 모달 전환 중에는 중복 명령을 막는다.
        public abstract bool IsDialogueTyping { get; }
        public abstract bool IsModalOpen { get; }
        public abstract bool IsModalBusy { get; }
        public abstract float ReactionHoldSeconds { get; }

        /// <summary>입력 의도를 전달할 컨트롤러를 연결한다. 재초기화 시 기존 입력 연결을 정리해야 한다.</summary>
        public abstract void Initialize(InspectionFlowController owner);
        /// <summary>진행 중인 표현을 취소하고 시작 시점의 화면으로 복원한다.</summary>
        public abstract void ResetPresentation();
        /// <summary>다음 방문자의 표시 데이터를 연결한다.</summary>
        public abstract void BindNpc(InspectionNpcData npc);
        /// <summary>Text Animator로 새 대사를 표시한다.</summary>
        public abstract void ShowDialogue(string text);
        /// <summary>현재 타이핑만 완료하며 다음 대사로 진행하지 않는다.</summary>
        public abstract void CompleteDialogue();
        /// <summary>현재 단계에서 가능한 행동을 안내한다.</summary>
        public abstract void SetHint(string text);
        /// <summary>컨트롤러의 상태를 받아 표시와 입력 허용 여부를 갱신한다.</summary>
        public abstract void SetState(InspectionState state);
        /// <summary>방문자 입장과 검사 책상으로의 시점 이동이 끝날 때까지 기다린다.</summary>
        public abstract IEnumerator EnterNpc(bool animate);
        /// <summary>서류 전달 연출이 끝나기 전에는 검사 입력을 열지 않는다.</summary>
        public abstract IEnumerator HandoffItems(bool animate);
        /// <summary>동일한 문서 원본의 확대/복귀를 완료한다. 전환 도중 입력은 잠근다.</summary>
        public abstract IEnumerator SetInspectionOpen(bool open, bool animate);
        /// <summary>확정된 판정을 표현하고 서류를 회수한다. 판정 자체는 변경하지 않는다.</summary>
        public abstract IEnumerator ResolveDocuments(InspectionDecision decision, bool animate);
        /// <summary>방문자 퇴장과 표시 데이터 정리가 끝날 때까지 기다린다.</summary>
        public abstract IEnumerator ExitNpc(bool animate);
        /// <summary>방문자 순회가 끝난 화면을 표시한다.</summary>
        public abstract void ShowCompleted();
        /// <summary>트윈·타이핑·포인터 점유를 취소하고 확대 원본을 복원한다. 재호출되어도 안전해야 한다.</summary>
        public abstract void CancelActivity();
    }
}
