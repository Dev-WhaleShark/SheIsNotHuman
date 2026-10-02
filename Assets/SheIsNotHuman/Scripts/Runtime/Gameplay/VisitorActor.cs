using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WhaleShark.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class VisitorActor : MonoBehaviour
    {
        [TabGroup("InspectorTabs", "연결"), FoldoutGroup("InspectorTabs/연결/참조", Expanded = false), SerializeField, Required] private RectTransform root;
        [FoldoutGroup("InspectorTabs/연결/참조"), SerializeField, Required] private CanvasGroup group;
        [FoldoutGroup("InspectorTabs/연결/참조"), SerializeField, Required] private Image portrait;
        [FoldoutGroup("InspectorTabs/연결/참조"), SerializeField, Required] private TMP_Text nameLabel;
        [TabGroup("InspectorTabs", "설정"), SerializeField, MinValue(0), LabelText("이동 시간 (초)")] private float motionSeconds = .35f;
        [ShowInInspector, ReadOnly, PropertyOrder(-10), LabelText("이동 중")] public bool IsMoving => tween != null && tween.IsActive();
        [ShowInInspector, ReadOnly, PropertyOrder(-9), LabelText("현재 방문자")] private string DisplayedName => nameLabel == null ? string.Empty : nameLabel.text;
        private Tween tween;
        private Vector2 origin;
        private bool captured;
        private int version;
        private void Awake() => CaptureOrigin();
        private void CaptureOrigin() { if (!captured && root != null) { origin = root.anchoredPosition; captured = true; } }
        public void Bind(string displayName, string id, Color color)
        {
            Cancel(); CaptureOrigin();
            if (nameLabel != null) nameLabel.text = displayName + "  /  " + id;
            if (portrait != null) portrait.color = color;
        }
        public IEnumerator Enter(bool animate) => Move(true, animate);
        public IEnumerator Exit(bool animate) => Move(false, animate);
        private IEnumerator Move(bool entering, bool animate)
        {
            Cancel(); CaptureOrigin();
            if (root == null || group == null || !isActiveAndEnabled) yield break;
            int current = version;
            if (entering) { root.anchoredPosition = origin + Vector2.left * 600; group.alpha = 0; }
            Vector2 target = entering ? origin : origin + Vector2.right * 600;
            if (animate && motionSeconds > 0)
            {
                tween = DOTween.Sequence().Join(root.DOAnchorPos(target, motionSeconds))
                    .Join(group.DOFade(entering ? 1 : 0, motionSeconds)).SetUpdate(true).SetLink(gameObject);
                yield return tween.WaitForCompletion();
            }
            if (current != version || !isActiveAndEnabled) yield break;
            tween = null;
            group.alpha = entering ? 1 : 0;
            root.anchoredPosition = origin;
            if (!entering) { if (nameLabel != null) nameLabel.text = string.Empty; if (portrait != null) portrait.color = Color.clear; }
        }
        [TabGroup("InspectorTabs", "개발 도구"), Button("방문자 초기화"), DisableInEditorMode]
        public void ResetActor()
        {
            Cancel(); CaptureOrigin();
            if (root != null) root.anchoredPosition = origin;
            if (group != null) group.alpha = 0;
            if (nameLabel != null) nameLabel.text = string.Empty;
            if (portrait != null) portrait.color = Color.clear;
        }
        public void Cancel() { version++; tween?.Kill(false); tween = null; }
        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();
    }
}
