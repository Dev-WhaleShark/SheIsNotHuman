using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace SheIsNotHuman.InspectionMvp
{
    [DisallowMultipleComponent]
    public sealed class IdentityDocumentView : MonoBehaviour
    {
        [Required] public TMP_Text content;
        public UnityEngine.UI.Image portrait;
        public bool expanded;
        private InspectionNpcData boundNpc;

        public void SetExpanded(bool value) { expanded = value; Bind(boundNpc); }

        public void Bind(InspectionNpcData npc)
        {
            boundNpc = npc;
            if (npc == null || npc.identity == null) { Clear(); return; }
            var data = npc.identity;
            content.text = expanded
                ? "신분증\n\n이름  " + data.displayName + "\n\n고객 코드\n" + data.customerCode
                : "신분증\n" + data.displayName + "\n" + data.customerCode;
            if (portrait != null)
            {
                portrait.color = npc.portraitColor;
                portrait.gameObject.SetActive(expanded);
            }
        }

        [Button] public void Clear()
        {
            boundNpc = null;
            if (content != null) content.text = string.Empty;
            if (portrait != null)
            {
                portrait.color = Color.clear;
                portrait.gameObject.SetActive(false);
            }
        }
    }
}
