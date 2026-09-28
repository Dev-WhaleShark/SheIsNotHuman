using System;
using Sirenix.OdinInspector;

namespace SheIsNotHuman.InspectionMvp
{
    /// <summary>플레이어 선택과 규칙의 기대 판정이 함께 사용하는 결과 값이다.</summary>
    public enum InspectionDecision { Pass, NonPass }

    /// <summary>입장 → 대화 → 서류 전달 → 검사 → 판정 → 퇴장을 거쳐 다음 방문자 또는 완료로 진행한다.</summary>
    public enum InspectionState
    {
        Initializing, NpcEntering, Dialogue, ItemHandoff,
        Inspecting, Resolving, NpcExiting, Completed
    }

    /// <summary>신분증의 표시 데이터이며 고객 코드만 판정 규칙에 참여한다.</summary>
    [Serializable]
    public sealed class IdentityDocument
    {
        [LabelText("이름")] public string displayName;
        [LabelText("고객 코드")] public string customerCode;
    }

    /// <summary>주문서의 표시 데이터이며 상품명·수량·주문 번호는 판정 조건이 아니다.</summary>
    [Serializable]
    public sealed class OrderDocument
    {
        [LabelText("주문 번호")] public string orderNumber;
        [LabelText("고객 코드")] public string customerCode;
        [LabelText("상품명")] public string productName;
        [LabelText("수량"), MinValue(1)] public int quantity = 1;
    }

    /// <summary>씬이나 표현 계층에 의존하지 않는 고객 코드 비교 규칙이다.</summary>
    public static class InspectionRule
    {
        /// <summary>두 코드가 비어 있지 않고 완전히 같을 때만 통과한다. 공백 제거나 대소문자 보정을 하지 않는다.</summary>
        public static InspectionDecision ExpectedDecision(InspectionNpcData npc)
        {
            if (npc == null || npc.identity == null || npc.order == null)
                return InspectionDecision.NonPass;

            string identityCode = npc.identity.customerCode;
            string orderCode = npc.order.customerCode;
            return !string.IsNullOrEmpty(identityCode)
                && !string.IsNullOrEmpty(orderCode)
                && string.Equals(identityCode, orderCode, StringComparison.Ordinal)
                    ? InspectionDecision.Pass
                    : InspectionDecision.NonPass;
        }
    }
}
