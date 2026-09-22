using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace SheIsNotHuman.InspectionMvp
{
    [DisallowMultipleComponent]
    public sealed class OrderDocumentView : MonoBehaviour
    {
        [Required] public TMP_Text content;
        public bool expanded;
        private InspectionNpcData boundNpc;

        public void SetExpanded(bool value) { expanded = value; Bind(boundNpc); }

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

        [Button] public void Clear() { boundNpc = null; if (content != null) content.text = string.Empty; }
    }
}
