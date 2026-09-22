using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace SheIsNotHuman.InspectionMvp
{
    /// <summary>주문서 원본의 간략/확대 표시를 담당한다. 판정은 표시 문자열이 아닌 원본 고객 코드를 사용한다.</summary>
    [DisallowMultipleComponent]
    public sealed class OrderDocumentView : MonoBehaviour
    {
        [Required] public TMP_Text content;
        public bool expanded;
        private InspectionNpcData boundNpc;

        /// <summary>복제 문서를 만들지 않고 현재 방문자의 내용을 같은 인스턴스에 다시 표시한다.</summary>
        public void SetExpanded(bool value) { expanded = value; Bind(boundNpc); }

        /// <summary>주문 정보를 표시하고, 누락된 데이터는 이전 내용이 남지 않도록 비운다.</summary>
        public void Bind(InspectionNpcData npc)
        {
            boundNpc = npc;
            if (npc == null || npc.order == null) { Clear(); return; }
            var data = npc.order;
            content.text = expanded
                ? "주문서\n\n주문 번호  " + data.orderNumber + "\n\n고객 코드  " + data.customerCode +
                  "\n\n상품  " + data.productName + "\n수량  " + data.quantity
                : "주문서\n" + data.orderNumber + "\n" + data.customerCode;
        }

        /// <summary>문서 내용과 재표시용 참조를 함께 해제한다.</summary>
        [Button] public void Clear() { boundNpc = null; if (content != null) content.text = string.Empty; }
    }
}
