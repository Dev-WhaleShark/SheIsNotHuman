using TMPro;
using UnityEngine;

namespace WhaleShark.Editor
{
    /// <summary>Small editor-only construction helpers used by the standalone desk harness.</summary>
    internal static class UIPrefabFactory
    {
        public static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));
            var rect=(RectTransform)go.transform; rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;
            rect.anchoredPosition=position; rect.sizeDelta=size; return rect;
        }
        public static TMP_Text Label(string name,Transform parent,string text,TMP_FontAsset font,Vector2 position,Vector2 size)
        {
            var rect=Rect(name,parent,position,size); var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font=font; label.text=text; label.fontSize=24; label.color=new Color(.12f,.16f,.19f);
            label.alignment=TextAlignmentOptions.Center; label.raycastTarget=false; return label;
        }
    }
}
