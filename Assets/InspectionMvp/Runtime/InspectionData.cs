using System;
using Sirenix.OdinInspector;

namespace SheIsNotHuman.InspectionMvp
{
    public enum InspectionDecision { Pass, NonPass }

    public enum InspectionState
    {
        Initializing, NpcEntering, Dialogue, ItemHandoff,
        Inspecting, Resolving, NpcExiting, Completed
    }

    [Serializable]
    public sealed class IdentityDocument
    {
        [LabelText("이름")] public string displayName;
        [LabelText("고객 코드")] public string customerCode;
    }

    [Serializable]
    public sealed class OrderDocument
    {
        [LabelText("주문 번호")] public string orderNumber;
        [LabelText("고객 코드")] public string customerCode;
        [LabelText("상품명")] public string productName;
        [LabelText("수량"), MinValue(1)] public int quantity = 1;
    }

    public static class InspectionRule
    {
        /// <summary>No trimming, case folding, or unrelated document fields affect the rule.</summary>
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
