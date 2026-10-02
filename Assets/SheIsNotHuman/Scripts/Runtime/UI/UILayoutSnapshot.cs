using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace WhaleShark.UI
{
    /// <summary>Captures the original subtree, including dropped position, typography and sibling order.</summary>
    public sealed class UILayoutSnapshot
    {
        public RectTransform Root { get; }
        public Vector3 WorldPosition { get; }
        public Quaternion WorldRotation { get; }
        public Vector3 WorldScale { get; }
        public Vector2 Size => rects[0].Size;
        private readonly RectState[] rects;
        private readonly TextState[] texts;
        private readonly GroupState[] groups;
        public UILayoutSnapshot(RectTransform root)
        {
            Root = root; WorldPosition = root.position; WorldRotation = root.rotation; WorldScale = root.lossyScale;
            var children = root.GetComponentsInChildren<RectTransform>(true);
            rects = new RectState[children.Length];
            for (int i = 0; i < children.Length; i++) rects[i] = new RectState(children[i]);
            var labels = root.GetComponentsInChildren<TMP_Text>(true);
            texts = new TextState[labels.Length];
            for (int i = 0; i < labels.Length; i++) texts[i] = new TextState(labels[i]);
            var canvases = root.GetComponentsInChildren<CanvasGroup>(true);
            groups = new GroupState[canvases.Length];
            for (int i = 0; i < canvases.Length; i++) groups[i] = new GroupState(canvases[i]);
        }
        public void Restore()
        {
            foreach (var saved in rects) saved.RestoreLayout();
            foreach (var saved in rects) saved.RestoreSibling();
            foreach (var saved in texts) saved.Restore();
            foreach (var saved in groups) saved.Restore();
        }
        // Return ALL roots before restoring indices; otherwise temporarily absent siblings shift indices.
        public static void RestoreAll(IReadOnlyList<UILayoutSnapshot> snapshots)
        {
            foreach (var snapshot in snapshots) snapshot.Restore();
            var roots = new List<RectState>();
            foreach (var snapshot in snapshots) roots.Add(snapshot.rects[0]);
            roots.Sort((a, b) => a.Sibling.CompareTo(b.Sibling));
            foreach (var root in roots) root.RestoreSibling();
        }
        private sealed class RectState
        {
            private readonly RectTransform rect;
            private readonly Transform parent;
            public readonly int Sibling;
            private readonly Vector2 min, max, pivot;
            public readonly Vector2 Size;
            private readonly Vector3 position, scale;
            private readonly Quaternion rotation;
            private readonly bool active;
            public RectState(RectTransform value)
            {
                rect = value; parent = value.parent; Sibling = value.GetSiblingIndex();
                min = value.anchorMin; max = value.anchorMax; pivot = value.pivot; Size = value.sizeDelta;
                position = value.anchoredPosition3D; scale = value.localScale; rotation = value.localRotation;
                active = value.gameObject.activeSelf;
            }
            public void RestoreLayout()
            {
                if (rect == null) return;
                rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
                rect.sizeDelta = Size; rect.anchoredPosition3D = position; rect.localRotation = rotation; rect.localScale = scale;
                rect.gameObject.SetActive(active);
            }
            public void RestoreSibling() { if (rect != null) rect.SetSiblingIndex(Sibling); }
        }
        private sealed class TextState
        {
            private readonly TMP_Text text;
            private readonly string content;
            private readonly float size, min, max;
            private readonly bool auto;
            private readonly Vector4 margin;
            private readonly TextAlignmentOptions alignment;
            public TextState(TMP_Text value)
            {
                text = value; content = value.text; size = value.fontSize; min = value.fontSizeMin; max = value.fontSizeMax;
                auto = value.enableAutoSizing; margin = value.margin; alignment = value.alignment;
            }
            public void Restore()
            {
                if (text == null) return;
                text.text = content; text.fontSize = size; text.fontSizeMin = min; text.fontSizeMax = max;
                text.enableAutoSizing = auto; text.margin = margin; text.alignment = alignment;
            }
        }
        private sealed class GroupState
        {
            private readonly CanvasGroup group;
            private readonly float alpha;
            private readonly bool interactable, raycasts, ignoreParents;
            public GroupState(CanvasGroup value)
            { group = value; alpha = value.alpha; interactable = value.interactable; raycasts = value.blocksRaycasts; ignoreParents = value.ignoreParentGroups; }
            public void Restore()
            {
                if (group == null) return;
                group.alpha = alpha; group.interactable = interactable; group.blocksRaycasts = raycasts; group.ignoreParentGroups = ignoreParents;
            }
        }
    }
}
