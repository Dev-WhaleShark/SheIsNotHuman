using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace WhaleShark.UI
{
    [DisallowMultipleComponent, MovedFrom(true, "SheIsNotHuman.InspectionMvp", null, "IdentityDocumentView")]
    public sealed class IdentityCard : MonoBehaviour
    {
        [TabGroup("InspectorTabs", "연결"), FoldoutGroup("InspectorTabs/연결/표시", Expanded = false), SerializeField]
        private TMP_Text content, displayNameText, customerCodeText, footerText;
        [FoldoutGroup("InspectorTabs/연결/표시"), SerializeField] private Image portrait;
        [SerializeField, HideInInspector, FormerlySerializedAs("expanded")] private bool focused;
        private IdentityCardData data;
        [ShowInInspector, ReadOnly, PropertyOrder(-20), LabelText("확대 표시")]
        public bool IsFocused => focused;
        [ShowInInspector, ReadOnly, PropertyOrder(-19), LabelText("표시 이름")]
        public string DisplayName => data?.displayName ?? string.Empty;
        public void Bind(IdentityCardData value) { data = value; Refresh(); }
        public void SetFocused(bool value) { focused = value; Refresh(); }
        private void Refresh()
        {
            bool authored = displayNameText != null || customerCodeText != null || footerText != null;
            if (displayNameText != null) displayNameText.text = data?.displayName ?? string.Empty;
            if (customerCodeText != null) customerCodeText.text = data?.customerCode ?? string.Empty;
            if (footerText != null) footerText.text = data?.footer ?? string.Empty;
            if (content != null && !authored) content.text = data == null ? string.Empty : focused
                ? "신분증\n\n이름  " + data.displayName + "\n\n고객 코드\n" + data.customerCode
                : "신분증\n" + data.displayName + "\n" + data.customerCode;
            if (portrait != null)
            {
                portrait.color = data == null ? Color.clear : data.portraitColor;
                portrait.gameObject.SetActive(data != null && (authored || focused));
            }
        }
        [TabGroup("InspectorTabs", "개발 도구"), Button("표시 지우기")]
        public void Clear() { data = null; Refresh(); }
    }
}
