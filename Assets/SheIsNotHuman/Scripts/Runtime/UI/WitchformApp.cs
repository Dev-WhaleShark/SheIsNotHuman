using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace WhaleShark.UI
{
    [DisallowMultipleComponent]
    public sealed class WitchformApp : MonoBehaviour
    {
        [TabGroup("InspectorTabs", "연결"), FoldoutGroup("InspectorTabs/연결/표시", Expanded = false), SerializeField]
        private TMP_Text content, customerNameText, customerCodeText, orderNumberText, productText, quantityText;
        private OrderDisplayData data;
        private bool focused;
        [ShowInInspector, ReadOnly, PropertyOrder(-20), LabelText("주문 번호")]
        public string OrderNumber => data?.orderNumber ?? string.Empty;
        public void Bind(OrderDisplayData value) { data = value; Refresh(); }
        public void SetFocused(bool value) { focused = value; Refresh(); }
        private void Refresh()
        {
            bool authored = customerNameText != null || customerCodeText != null || orderNumberText != null || productText != null || quantityText != null;
            if (customerNameText != null) customerNameText.text = data?.customerName ?? string.Empty;
            if (customerCodeText != null) customerCodeText.text = data?.customerCode ?? string.Empty;
            if (orderNumberText != null) orderNumberText.text = data?.orderNumber ?? string.Empty;
            if (productText != null) productText.text = data?.productName ?? string.Empty;
            if (quantityText != null) quantityText.text = data == null ? string.Empty : data.quantity.ToString();
            if (content != null) content.text = data == null || authored ? string.Empty : focused
                ? "주문서\n\n주문 번호  " + data.orderNumber + "\n\n고객 코드  " + data.customerCode + "\n\n상품  " + data.productName + "\n수량  " + data.quantity
                : "주문서\n" + data.orderNumber + "\n" + data.customerCode;
        }
        [TabGroup("InspectorTabs", "개발 도구"), Button("표시 지우기")]
        public void Clear() { data = null; Refresh(); }
    }
}
