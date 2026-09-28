using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace SheIsNotHuman.InspectionMvp
{
    /// <summary>신분증 원본의 간략/확대 표시를 담당하며 판정이나 방문자 진행은 변경하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class IdentityDocumentView : MonoBehaviour
    {
        [Required] public TMP_Text content;
        public TMP_Text displayNameText;
        public TMP_Text customerCodeText;
        public TMP_Text footerText;
        public UnityEngine.UI.Image portrait;
        public bool expanded;
        private InspectionNpcData boundNpc;
        private bool HasAuthoredLayout => displayNameText != null || customerCodeText != null || footerText != null;

        /// <summary>현재 방문자 참조를 유지한 채 같은 문서 인스턴스의 표시만 다시 구성한다.</summary>
        public void SetExpanded(bool value) { expanded = value; Bind(boundNpc); }

        /// <summary>원본 데이터에서 표시를 갱신한다. 데이터가 없으면 이전 방문자의 내용도 지운다.</summary>
        public void Bind(InspectionNpcData npc)
        {
            boundNpc = npc;
            if (npc == null || npc.identity == null) { Clear(); return; }
            var data = npc.identity;
            if (HasAuthoredLayout)
            {
                if (displayNameText != null) displayNameText.text = data.displayName;
                if (customerCodeText != null) customerCodeText.text = data.customerCode;
                if (footerText != null) footerText.text = npc.npcId;
            }
            else if (content != null) content.text = expanded
                ? "신분증\n\n이름  " + data.displayName + "\n\n고객 코드\n" + data.customerCode
                : "신분증\n" + data.displayName + "\n" + data.customerCode;
            if (portrait != null)
            {
                portrait.color = npc.portraitColor;
                portrait.gameObject.SetActive(HasAuthoredLayout || expanded);
            }
        }

        /// <summary>재표시에 쓰는 방문자 참조까지 해제하여 다음 방문자에게 이전 내용이 남지 않게 한다.</summary>
        [Button] public void Clear()
        {
            boundNpc = null;
            if (!HasAuthoredLayout && content != null) content.text = string.Empty;
            if (displayNameText != null) displayNameText.text = string.Empty;
            if (customerCodeText != null) customerCodeText.text = string.Empty;
            if (footerText != null) footerText.text = string.Empty;
            if (portrait != null)
            {
                portrait.color = Color.clear;
                portrait.gameObject.SetActive(false);
            }
        }
    }
}
