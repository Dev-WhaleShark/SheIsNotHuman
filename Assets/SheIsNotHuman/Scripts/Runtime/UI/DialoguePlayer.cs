using System;
using System.Collections;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;

namespace WhaleShark.UI
{
    [DisallowMultipleComponent]
    public sealed class DialoguePlayer : MonoBehaviour
    {
        [TabGroup("InspectorTabs", "연결"), FoldoutGroup("InspectorTabs/연결/참조", Expanded = false), SerializeField, Required]
        private DialoguePanel panel;
        [ShowInInspector, ReadOnly, PropertyOrder(-10), LabelText("재생 중")]
        public bool IsRunning { get; private set; }
        [ShowInInspector, ReadOnly, PropertyOrder(-9), LabelText("타이핑 중")]
        public bool IsTyping => panel != null && panel.IsTyping;
        [ShowInInspector, ReadOnly, PropertyOrder(-8), LabelText("대사 인덱스")]
        public int Index { get; private set; } = -1;
        [ShowInInspector, ReadOnly, PropertyOrder(-7), LabelText("대사 수")]
        public int Count => lines.Length;
        [TabGroup("InspectorTabs", "설정"), ShowInInspector, ReadOnly, LabelText("클릭 동작")]
        private string AdvanceBehavior => "타이핑 중에는 완성, 완성 후에는 다음 문장";
        private string[] lines = Array.Empty<string>();
        private readonly Subject<Unit> completed = new Subject<Unit>();
        public Observable<Unit> Completed => completed;
        private IDisposable advanceSubscription;
        private int version;
        private void OnEnable()
        {
            advanceSubscription?.Dispose();
            if (panel != null) advanceSubscription = panel.AdvanceRequested.Subscribe(_ => Advance());
        }
        private void OnDisable() { advanceSubscription?.Dispose(); advanceSubscription = null; Cancel(); }
        private void OnDestroy() { Cancel(); completed.Dispose(); }
        public void Play(string[] text)
        {
            Cancel();
            if (!isActiveAndEnabled || panel == null || !panel.isActiveAndEnabled) return;
            lines = text == null ? Array.Empty<string>() : (string[])text.Clone();
            IsRunning = true;
            if (lines.Length == 0) { Finish(); return; }
            Index = 0;
            panel.Show(lines[Index]);
        }
        public IEnumerator PlayAndWait(string[] text)
        {
            int current = version + 1;
            Play(text);
            while (current == version && IsRunning && isActiveAndEnabled && panel != null && panel.isActiveAndEnabled)
                yield return null;
            if (current == version && IsRunning) Cancel();
        }
        [TabGroup("InspectorTabs", "개발 도구"), Button("대사 진행 / 타이핑 완료"), DisableInEditorMode]
        public void Advance()
        {
            if (!isActiveAndEnabled || !IsRunning || panel == null || !panel.isActiveAndEnabled) return;
            if (IsTyping) { CompleteTyping(); return; }
            if (++Index < lines.Length) panel.Show(lines[Index]);
            else Finish();
        }
        public void CompleteTyping() { if (IsRunning && panel != null) panel.CompleteTyping(); }
        // Gameplay can await the panel animation without accessing its serialized UI connections.
        public IEnumerator Hide(bool animate)
        {
            Cancel();
            if (panel != null) yield return panel.Hide(animate);
        }
        private void Finish()
        {
            if (!IsRunning) return;
            IsRunning = false;
            completed.OnNext(Unit.Default);
        }
        public void Cancel()
        {
            version++;
            IsRunning = false; Index = -1; lines = Array.Empty<string>();
            if (panel != null) panel.Cancel();
        }
    }
}
