using System.Collections;
using DG.Tweening;
using Febucci.TextAnimatorForUnity;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace WhaleShark.UI
{
    [DisallowMultipleComponent]
    public sealed class DialoguePanel : MonoBehaviour
    {
        [ShowInInspector, ReadOnly, PropertyOrder(-10), LabelText("타이핑 중")]
        public bool IsTyping => writer != null && writer.IsShowingText;
        [ShowInInspector, ReadOnly, PropertyOrder(-9), LabelText("입력 허용")]
        public bool IsInputEnabled => inputEnabled;
        [TabGroup("InspectorTabs", "설정"), SerializeField, MinValue(.1f), LabelText("타이핑 속도")]
        private float typingSpeed = 1f;
        [TabGroup("InspectorTabs", "설정"), SerializeField, MinValue(0), LabelText("숨김 시간 (초)")]
        private float hideSeconds = .22f;
        [TabGroup("InspectorTabs", "설정"), SerializeField, LabelText("숨김 이동 거리")]
        private Vector2 hideOffset = new Vector2(0, 18);
        [TabGroup("InspectorTabs", "연결"), FoldoutGroup("InspectorTabs/연결/참조", Expanded = false), SerializeField, Required]
        private TypewriterComponent writer;
        [FoldoutGroup("InspectorTabs/연결/참조"), SerializeField, Required] private Button button;
        [FoldoutGroup("InspectorTabs/연결/참조"), SerializeField, Required] private CanvasGroup group;

        private readonly Subject<Unit> advanceRequested = new Subject<Unit>();
        public Observable<Unit> AdvanceRequested => advanceRequested;
        private Tween hideTween;
        private int version;
        private RectTransform rect;
        private Vector2 origin;
        private bool captured;
        private bool inputEnabled;

        private void Awake() => CaptureOrigin();
        private void CaptureOrigin()
        {
            if (captured || button == null) return;
            rect = button.transform as RectTransform;
            if (rect == null) return;
            origin = rect.anchoredPosition;
            captured = true;
        }
        private void OnEnable() { if (button != null) button.onClick.AddListener(RequestAdvance); }
        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(RequestAdvance);
            Cancel();
        }
        private void OnDestroy() { Cancel(); advanceRequested.Dispose(); }
        private void RequestAdvance()
        {
            if (isActiveAndEnabled && inputEnabled && button != null && button.interactable)
                advanceRequested.OnNext(Unit.Default);
        }
        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (button != null) button.interactable = enabled;
        }
        public void Show(string text)
        {
            Cancel(); CaptureOrigin();
            if (button != null) button.gameObject.SetActive(true);
            if (rect != null) rect.anchoredPosition = origin;
            if (group != null) group.alpha = 1;
            if (group != null) group.blocksRaycasts = true;
            if (writer == null) return;
            writer.SetTypewriterSpeed(typingSpeed);
            writer.ShowText(text ?? string.Empty);
        }
        public void CompleteTyping() { if (writer != null) writer.SkipTypewriter(); }
        public IEnumerator Hide(bool animate)
        {
            Cancel(); CaptureOrigin();
            int current = version;
            SetInputEnabled(false);
            if (group != null) group.blocksRaycasts = false;
            if (animate && group != null && rect != null && hideSeconds > 0)
            {
                hideTween = DOTween.Sequence().Join(group.DOFade(0, hideSeconds))
                    .Join(rect.DOAnchorPos(origin + hideOffset, hideSeconds)).SetUpdate(true).SetLink(gameObject);
                yield return hideTween.WaitForCompletion();
            }
            if (current != version || !isActiveAndEnabled) yield break;
            hideTween = null;
            if (group != null) group.alpha = 0;
            if (rect != null) rect.anchoredPosition = origin + hideOffset;
            // Do not disable the panel's own GameObject: the independent player must stay subscribed.
            if (button != null && button.gameObject != gameObject) button.gameObject.SetActive(false);
        }
        public void Clear()
        {
            Cancel(); CaptureOrigin();
            if (writer != null) { writer.ShowText(string.Empty); writer.StopShowingText(); }
            if (rect != null) rect.anchoredPosition = origin;
            if (group != null) group.alpha = 0;
            SetInputEnabled(false);
            if (group != null) group.blocksRaycasts = false;
        }
        public void Cancel()
        {
            version++;
            hideTween?.Kill(false); hideTween = null;
            if (writer != null) { writer.StopShowingText(); writer.StopDisappearingText(); }
        }
        [TabGroup("InspectorTabs", "개발 도구"), Button("타이핑 완료"), DisableInEditorMode]
        private void DebugComplete() { if (isActiveAndEnabled) CompleteTyping(); }
    }
}
