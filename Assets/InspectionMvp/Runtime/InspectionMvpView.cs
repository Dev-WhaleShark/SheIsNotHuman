using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Febucci.TextAnimatorForUnity;
using SheIsNotHuman.CubeScreen;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace SheIsNotHuman.InspectionMvp
{
    /// <summary>Presentation only: the flow controller owns every gameplay decision.</summary>
    public sealed class InspectionMvpView : InspectionPresentation
    {
        [Title("Scene wiring"), Required] public PerspectiveCubeViewController navigation;
        [Required] public RectTransform npcRoot, documentsRoot, modalPanel;
        [Required] public CanvasGroup npcGroup, documentsGroup, modalGroup;
        [Required] public GameObject modalShield;
        [Required] public UnityEngine.UI.Image portrait, identityPortrait;
        [Required] public TMP_Text npcName, hint, stateLabel, identitySummary, orderSummary, identityDetail, orderDetail, resultStamp;
        [Required] public TypewriterComponent dialogueWriter;
        [Required] public UnityEngine.UI.Button dialogueButton, inspectButton, identityButton, orderButton, closeButton, passButton, nonPassButton, restartButton, deskButton, frontButton;
        [Title("Independent document views"), Required] public IdentityDocumentView identityDocument, expandedIdentityDocument;
        [Required] public OrderDocumentView orderDocument, expandedOrderDocument;
        [Title("Desk items")] public DeskInspectableItem[] deskItems = new DeskInspectableItem[0];
        [Required] public GameObject dummyPanel;
        [Required] public TMP_Text dummyTitle;
        [Required] public UnityEngine.UI.Image dummyImage;
        [Required] public UnityEngine.UI.Button dummyCloseButton;
        [Title("Motion"), MinValue(0)] public float motionSeconds = .35f;
        [MinValue(0)] public float modalSeconds = .2f;
        [MinValue(0)] public float reactionHoldSeconds = 1.2f;
        [MinValue(.1)] public float typingSpeed = 1f;
        [Title("Original object focus"), Required] public Canvas focusCanvas;
        [Required] public RectTransform focusItemsRoot, focusControls;
        [Required] public CanvasGroup dialogueGroup;
        [MinValue(0)] public float dialogueHideSeconds = .22f;
        public Vector2 dialogueHideOffset = new Vector2(0, 18);
        [Range(.2f, .9f)] public float focusDepthRatio = .7f;
        public Canvas FocusCanvas => focusCanvas;
        public CanvasGroup DialogueGroup => dialogueGroup;
        public bool IsFocusActive => focused.Count > 0;
        public GameObject FocusedIdentity => IsFocusActive && !dummyMode ? identityButton.gameObject : null;
        public GameObject FocusedOrder => IsFocusActive && !dummyMode ? orderButton.gameObject : null;
        public GameObject FocusedDummy => IsFocusActive && dummyMode && selectedDummy != null ? selectedDummy.gameObject : null;
        [ShowInInspector, ReadOnly] public override bool IsModalOpen => modalOpen && !dummyMode;
        [ShowInInspector, ReadOnly] public override bool IsModalBusy => modalBusy || dummyMode;
        public bool IsAnyModalOpen => modalOpen;
        public override bool IsDialogueTyping => dialogueWriter != null && dialogueWriter.IsShowingText;
        public override float ReactionHoldSeconds => reactionHoldSeconds;

        private InspectionFlowController flow;
        private InspectionState state;
        private bool modalOpen, modalBusy, motionBusy;
        private Tween activeTween;
        private Vector2 npcOrigin, documentsOrigin;
        private bool initialized;
        private bool dummyMode;
        private Coroutine dummyRoutine;
        private DeskInspectableItem selectedDummy;
        private readonly List<FocusSnapshot> focused = new List<FocusSnapshot>();
        private Tween dialogueTween;
        private Vector2 dialogueOrigin;
        private int activityVersion;
        private int dialogueVersion;

        public override void Initialize(InspectionFlowController owner)
        {
            if (initialized) Unwire();
            flow = owner;
            if (!initialized)
            {
                npcOrigin = npcRoot.anchoredPosition; documentsOrigin = documentsRoot.anchoredPosition;
                dialogueOrigin = ((RectTransform)dialogueButton.transform).anchoredPosition;
            }
            dialogueButton.onClick.AddListener(Advance);
            inspectButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            if (dummyCloseButton != null && dummyCloseButton != closeButton) dummyCloseButton.onClick.AddListener(Close);
            passButton.onClick.AddListener(Pass);
            nonPassButton.onClick.AddListener(NonPass);
            restartButton.onClick.AddListener(RestartFlow);
            deskButton.onClick.AddListener(FocusDesk);
            frontButton.onClick.AddListener(FocusFront);
            initialized = true;
            ResetPresentation();
        }

        private bool Stable => !motionBusy && !modalBusy && (navigation == null || !navigation.IsTurning);
        private bool OnFace(CubeFace face) => navigation == null || (navigation.isActiveAndEnabled && !navigation.IsTurning && navigation.CurrentFace == face);
        private void Advance() { if (OnFace(CubeFace.Bottom) && Stable && !modalOpen && !DeskInspectableItem.AnyPointerInteraction) flow.AdvanceDialogue(); }
        private void Open() { if (OnFace(CubeFace.Bottom) && Stable && !modalOpen && !DeskInspectableItem.AnyPointerInteraction) flow.OpenInspection(); }
        private void Close()
        {
            if (!OnFace(CubeFace.Bottom) || !Stable || !modalOpen) return;
            if (dummyMode) dummyRoutine = StartCoroutine(CloseDummy());
            else flow.CloseInspection();
        }
        private void Pass() { if (OnFace(CubeFace.Bottom) && Stable && modalOpen && !dummyMode) flow.Decide(InspectionDecision.Pass); }
        private void NonPass() { if (OnFace(CubeFace.Bottom) && Stable && modalOpen && !dummyMode) flow.Decide(InspectionDecision.NonPass); }
        private void RestartFlow() { if (OnFace(CubeFace.Bottom) && flow != null) flow.Restart(); }
        private void FocusDesk() { if (OnFace(CubeFace.Front) && Stable && !modalOpen && !DeskInspectableItem.AnyPointerInteraction) navigation.FocusFace(CubeFace.Bottom, flow.animateTransitions); }
        private void FocusFront() { if (OnFace(CubeFace.Bottom) && Stable && !modalOpen && !DeskInspectableItem.AnyPointerInteraction) navigation.FocusFace(CubeFace.Front, flow.animateTransitions); }

        public bool CanInteractWith(DeskInspectableItem item) => item != null && initialized && isActiveAndEnabled && Stable && !modalOpen
            && (navigation == null || (navigation.isActiveAndEnabled && navigation.CurrentFace == CubeFace.Bottom && !navigation.IsTurning))
            && (DeskInspectableItem.ActiveItem == null || DeskInspectableItem.ActiveItem == item)
            && (item.kind == DeskItemKind.Document ? state == InspectionState.Inspecting
                : state == InspectionState.Dialogue || state == InspectionState.Inspecting || state == InspectionState.Completed);

        public void ExpandItem(DeskInspectableItem item)
        {
            if (!CanInteractWith(item)) return;
            if (item.kind == DeskItemKind.Document) { Open(); return; }
            CancelDeskGestures();
            dummyMode = true;
            selectedDummy = item;
            dummyRoutine = StartCoroutine(SetInspectionOpen(true, flow.animateTransitions));
        }

        private IEnumerator CloseDummy()
        {
            int version = activityVersion;
            yield return SetInspectionOpen(false, flow.animateTransitions);
            if (version != activityVersion || !isActiveAndEnabled) yield break;
            dummyMode = false;
            selectedDummy = null;
            dummyRoutine = null;
            RefreshInput();
        }
        private void CancelDeskGestures()
        {
            foreach (var item in deskItems) if (item != null) item.CancelInteraction();
        }
        private void RestoreDeskItems(bool documentsOnly)
        {
            foreach (var item in deskItems)
                if (item != null && (!documentsOnly || item.kind == DeskItemKind.Document)) item.RestorePosition();
        }
        public void RefreshDeskInput() { if (initialized) RefreshInput(); }

        private void Update() { if (initialized) RefreshInput(); }
        private void RefreshInput()
        {
            bool stable = Stable && !DeskInspectableItem.AnyPointerInteraction && OnFace(CubeFace.Bottom);
            bool inspecting = state == InspectionState.Inspecting;
            dialogueButton.interactable = stable && !modalOpen && state == InspectionState.Dialogue;
            inspectButton.interactable = stable && !modalOpen && inspecting;
            identityButton.interactable = orderButton.interactable = inspectButton.interactable;
            closeButton.interactable = stable && modalOpen && (inspecting || dummyMode);
            if (dummyCloseButton != null && dummyCloseButton != closeButton) dummyCloseButton.interactable = stable && modalOpen && dummyMode;
            passButton.interactable = nonPassButton.interactable = stable && modalOpen && inspecting && !dummyMode;
            deskButton.interactable = OnFace(CubeFace.Front) && Stable && !DeskInspectableItem.AnyPointerInteraction && !modalOpen && (inspecting || state == InspectionState.Dialogue);
            frontButton.interactable = stable && !modalOpen && (inspecting || state == InspectionState.Dialogue);
            restartButton.interactable = stable && !modalOpen;
            if (navigation != null)
                navigation.InputBlocked = modalOpen || modalBusy || motionBusy || DeskInspectableItem.AnyPointerInteraction
                    || (state != InspectionState.Dialogue && state != InspectionState.Inspecting && state != InspectionState.Completed);
        }

        public override void ResetPresentation()
        {
            CancelActivity();
            modalOpen = modalBusy = motionBusy = false;
            modalShield.SetActive(false);
            dummyMode = false;
            if (dummyPanel != null) dummyPanel.SetActive(false);
            modalPanel.gameObject.SetActive(false);
            modalGroup.alpha = 0;
            modalPanel.localScale = Vector3.one;
            npcRoot.anchoredPosition = npcOrigin;
            documentsRoot.anchoredPosition = documentsOrigin;
            RestoreDeskItems(false);
            npcGroup.alpha = documentsGroup.alpha = 0;
            documentsGroup.interactable = documentsGroup.blocksRaycasts = false;
            dialogueWriter.ShowText(string.Empty);
            RestoreDialogue();
            ClearNpcData();
            restartButton.gameObject.SetActive(false);
            SetState(InspectionState.Initializing);
        }

        public override void BindNpc(InspectionNpcData npc)
        {
            RestoreFocusedItems();
            npcName.text = npc.displayName + "  /  " + npc.npcId;
            portrait.color = npc.portraitColor;
            resultStamp.gameObject.SetActive(false);
            identityDocument.Bind(npc);
            orderDocument.Bind(npc);
            RestoreDeskItems(true);
            npcRoot.anchoredPosition = npcOrigin;
            documentsRoot.anchoredPosition = documentsOrigin;
            documentsGroup.alpha = 0;
            documentsGroup.interactable = documentsGroup.blocksRaycasts = false;
        }

        public override void ShowDialogue(string text)
        {
            RestoreDialogue();
            dialogueWriter.StopShowingText();
            dialogueWriter.SetTypewriterSpeed(typingSpeed);
            dialogueWriter.ShowText(text ?? string.Empty);
        }
        public override void CompleteDialogue() => dialogueWriter.SkipTypewriter();
        public override void SetHint(string text) => hint.text = text;
        public override void SetState(InspectionState value)
        {
            if (state != value)
            {
                CancelDeskGestures();
                if (dummyMode) HideDummyImmediately();
            }
            state = value;
            stateLabel.text = value == InspectionState.Completed ? "프로토타입 완료" : "검수 창구  /  " + (flow == null ? 0 : flow.ResolvedCount) + "명 처리";
            RefreshInput();
        }

        private IEnumerator Focus(CubeFace face, bool animate)
        {
            navigation.FocusFace(face, animate);
            while (navigation.IsTurning) yield return null;
        }

        public override IEnumerator EnterNpc(bool animate)
        {
            int version = activityVersion;
            motionBusy = true; RefreshInput();
            yield return Focus(CubeFace.Front, animate);
            if (version != activityVersion || !isActiveAndEnabled) yield break;
            npcRoot.anchoredPosition = npcOrigin + Vector2.left * 600;
            npcGroup.alpha = 0;
            if (animate)
                yield return Play(DOTween.Sequence().Join(npcRoot.DOAnchorPos(npcOrigin, motionSeconds)).Join(npcGroup.DOFade(1, motionSeconds)));
            if (version != activityVersion || !isActiveAndEnabled) yield break;
            npcRoot.anchoredPosition = npcOrigin; npcGroup.alpha = 1;
            yield return Focus(CubeFace.Bottom, animate);
            if (version != activityVersion || !isActiveAndEnabled) yield break;
            motionBusy = false; RefreshInput();
        }

        public override IEnumerator HandoffItems(bool animate)
        {
            int version = activityVersion;
            motionBusy = true; RefreshInput();
            yield return HideDialogue(animate);
            if (version != activityVersion || !isActiveAndEnabled) yield break;
            yield return Focus(CubeFace.Bottom, animate);
            if (version != activityVersion || !isActiveAndEnabled) yield break;
            documentsRoot.anchoredPosition = documentsOrigin + Vector2.up * 180;
            documentsGroup.alpha = 0;
            if (animate)
                yield return Play(DOTween.Sequence().Join(documentsRoot.DOAnchorPos(documentsOrigin, motionSeconds)).Join(documentsGroup.DOFade(1, motionSeconds)));
            if (version != activityVersion || !isActiveAndEnabled) yield break;
            documentsRoot.anchoredPosition = documentsOrigin; documentsGroup.alpha = 1;
            documentsGroup.interactable = documentsGroup.blocksRaycasts = true;
            motionBusy = false; RefreshInput();
        }

        public override IEnumerator SetInspectionOpen(bool open, bool animate)
        {
            int version = activityVersion;
            if (modalBusy || modalOpen == open) yield break;
            CancelDeskGestures();
            modalBusy = true; RefreshInput();
            if (open)
            {
                yield return Focus(CubeFace.Bottom, animate);
                if (version != activityVersion || !isActiveAndEnabled) yield break;
                modalOpen = true;
                PrepareFocus();
                modalPanel.gameObject.SetActive(false);
                if (dummyPanel != null) dummyPanel.SetActive(false);
                modalShield.transform.SetAsLastSibling();
                modalShield.SetActive(true);
                modalGroup.alpha = 0;
            }
            var sequence = DOTween.Sequence();
            sequence.Join(modalGroup.DOFade(open ? 1 : 0, animate ? modalSeconds : 0));
            foreach (var snapshot in focused)
                snapshot.Animate(sequence, open, animate ? modalSeconds : 0);
            if (animate)
                yield return Play(sequence);
            else sequence.Complete(true);
            if (version != activityVersion || !isActiveAndEnabled) yield break;
            modalGroup.alpha = open ? 1 : 0;
            if (!open) RestoreFocusedItems();
            modalOpen = open;
            modalShield.SetActive(open);
            modalBusy = false; RefreshInput();
        }

        public override IEnumerator ResolveDocuments(InspectionDecision decision, bool animate)
        {
            int version = activityVersion;
            motionBusy = true; RefreshInput();
            yield return SetInspectionOpen(false, animate);
            if (version != activityVersion || !isActiveAndEnabled) yield break;
            if (animate)
                yield return Play(DOTween.Sequence().Join(documentsRoot.DOAnchorPos(documentsOrigin + Vector2.up * 220, motionSeconds))
                    .Join(documentsGroup.DOFade(0, motionSeconds)));
            if (version != activityVersion || !isActiveAndEnabled) yield break;
            documentsGroup.alpha = 0;
            documentsGroup.interactable = documentsGroup.blocksRaycasts = false;
            documentsRoot.anchoredPosition = documentsOrigin;
            // The result remains visible on the desk while the controller shows the NPC reaction.
            resultStamp.text = decision == InspectionDecision.Pass ? "PASS" : "NON PASS";
            resultStamp.color = decision == InspectionDecision.Pass ? new Color(.1f,.55f,.3f) : new Color(.75f,.18f,.12f);
            resultStamp.gameObject.SetActive(true);
            resultStamp.rectTransform.localScale = Vector3.one;
            if (animate)
            {
                resultStamp.rectTransform.localScale = Vector3.one * 1.3f;
                yield return Play(resultStamp.rectTransform.DOScale(1, modalSeconds));
                if (version != activityVersion || !isActiveAndEnabled) yield break;
            }
            motionBusy = false; RefreshInput();
        }

        public override IEnumerator ExitNpc(bool animate)
        {
            int version = activityVersion;
            motionBusy = true; RefreshInput();
            yield return HideDialogue(animate);
            if (version != activityVersion || !isActiveAndEnabled) yield break;
            yield return Focus(CubeFace.Front, animate);
            if (version != activityVersion || !isActiveAndEnabled) yield break;
            if (animate)
                yield return Play(DOTween.Sequence().Join(npcRoot.DOAnchorPos(npcOrigin + Vector2.right * 600, motionSeconds))
                    .Join(npcGroup.DOFade(0, motionSeconds)));
            if (version != activityVersion || !isActiveAndEnabled) yield break;
            npcGroup.alpha = 0; npcRoot.anchoredPosition = npcOrigin;
            ClearNpcData();
            motionBusy = false; RefreshInput();
        }

        public override void ShowCompleted()
        {
            CancelActivity();
            modalOpen = modalBusy = motionBusy = false;
            modalShield.SetActive(false);
            navigation.FocusFace(CubeFace.Bottom, false);
            ShowDialogue("프로토타입 완료"); CompleteDialogue();
            SetHint("다시 시작하여 같은 순서로 재검수할 수 있습니다.");
            restartButton.gameObject.SetActive(true);
            RefreshInput();
        }

        private IEnumerator Play(Tween tween)
        {
            int version = activityVersion;
            activeTween = tween.SetUpdate(true).SetLink(gameObject);
            yield return activeTween.WaitForCompletion();
            if (version == activityVersion) activeTween = null;
        }
        public override void CancelActivity()
        {
            activityVersion++;
            CancelDeskGestures();
            HideDummyImmediately();
            activeTween?.Kill(false); activeTween = null;
            dialogueTween?.Kill(false); dialogueTween = null;
            RestoreFocusedItems();
            if (dialogueWriter != null) { dialogueWriter.StopShowingText(); dialogueWriter.StopDisappearingText(); }
            motionBusy = modalBusy = modalOpen = false;
            if (modalShield != null) modalShield.SetActive(false);
            if (modalGroup != null) modalGroup.alpha = 0;
            if (navigation != null)
            {
                if (navigation.IsTurning) navigation.FocusFace(navigation.CurrentFace, false);
                navigation.InputBlocked = false;
            }
        }
        private void HideDummyImmediately()
        {
            if (dummyRoutine != null) { StopCoroutine(dummyRoutine); dummyRoutine = null; }
            if (!dummyMode) return;
            activeTween?.Kill(false); activeTween = null;
            RestoreFocusedItems();
            dummyMode = modalOpen = modalBusy = false;
            selectedDummy = null;
            if (dummyPanel != null) dummyPanel.SetActive(false);
            modalPanel.gameObject.SetActive(false);
            modalShield.SetActive(false);
            modalGroup.alpha = 0;
        }
        private void RestoreDialogue()
        {
            dialogueVersion++;
            dialogueTween?.Kill(false); dialogueTween = null;
            dialogueButton.gameObject.SetActive(true);
            ((RectTransform)dialogueButton.transform).anchoredPosition = dialogueOrigin;
            if (dialogueGroup != null) dialogueGroup.alpha = 1;
        }

        private IEnumerator HideDialogue(bool animate)
        {
            int version = activityVersion;
            int hideVersion = ++dialogueVersion;
            dialogueTween?.Kill(false);
            var rect = (RectTransform)dialogueButton.transform;
            if (animate && dialogueGroup != null)
            {
                dialogueTween = DOTween.Sequence().Join(dialogueGroup.DOFade(0, dialogueHideSeconds))
                    .Join(rect.DOAnchorPos(dialogueOrigin + dialogueHideOffset, dialogueHideSeconds))
                    .SetUpdate(true).SetLink(gameObject);
                yield return dialogueTween.WaitForCompletion();
            }
            if (version != activityVersion || hideVersion != dialogueVersion || !isActiveAndEnabled) yield break;
            dialogueTween = null;
            if (dialogueGroup != null) dialogueGroup.alpha = 0;
            rect.anchoredPosition = dialogueOrigin + dialogueHideOffset;
            dialogueButton.gameObject.SetActive(false);
        }

        private void PrepareFocus()
        {
            RestoreFocusedItems();
            // Capture every original before changing any sibling indices.
            if (dummyMode) focused.Add(new FocusSnapshot((RectTransform)selectedDummy.transform));
            else
            {
                focused.Add(new FocusSnapshot((RectTransform)identityButton.transform));
                focused.Add(new FocusSnapshot((RectTransform)orderButton.transform));
            }
            var camera = navigation.ViewCamera;
            float depth = float.PositiveInfinity;
            foreach (var snapshot in focused)
                depth = Mathf.Min(depth, Vector3.Dot(snapshot.Rect.position - camera.transform.position, camera.transform.forward));
            depth = Mathf.Max(camera.nearClipPlane + .05f, depth * focusDepthRatio);
            float height = Vector3.Distance(camera.ViewportToWorldPoint(new Vector3(.5f, 0, depth)),
                camera.ViewportToWorldPoint(new Vector3(.5f, 1, depth)));
            float width = height * camera.aspect;
            float scale = Mathf.Min(width / 1200f, height / 675f);
            var canvasRect = (RectTransform)focusCanvas.transform;
            canvasRect.SetPositionAndRotation(camera.ViewportToWorldPoint(new Vector3(.5f, .5f, depth)), camera.transform.rotation);
            canvasRect.localScale = Vector3.one * scale;
            canvasRect.sizeDelta = new Vector2(width / scale, height / scale);
            focusCanvas.worldCamera = camera;
            focusCanvas.GetComponent<DistortionCorrectedGraphicRaycaster>().ConfigureFaceGate(navigation, CubeFace.Bottom);
            focusCanvas.gameObject.SetActive(true);
            focusControls.gameObject.SetActive(true);
            passButton.gameObject.SetActive(!dummyMode);
            nonPassButton.gameObject.SetActive(!dummyMode);
            for (int i = 0; i < focused.Count; i++)
            {
                Vector2 size = dummyMode ? focused[i].Rect.rect.size : new Vector2(460, 380);
                float itemScale = dummyMode ? Mathf.Min(720f / Mathf.Max(1, size.x), 330f / Mathf.Max(1, size.y)) : 1;
                focused[i].Prepare(focusItemsRoot, dummyMode ? new Vector2(0, 20) : new Vector2(i == 0 ? -250 : 250, 20), size, itemScale);
            }
            if (!dummyMode)
            {
                identityDocument.SetExpanded(true);
                orderDocument.SetExpanded(true);
                FormatFocusedDocument(identityDocument.content, identityDocument.portrait != null);
                FormatFocusedDocument(orderDocument.content);
            }
            focusControls.SetAsLastSibling();
        }

        private static void FormatFocusedDocument(TMP_Text text, bool reservePortrait = false)
        {
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one * .5f;
            rect.offsetMin = new Vector2(24, 20);
            rect.offsetMax = new Vector2(reservePortrait ? -150 : -24, -20);
            text.enableAutoSizing = true; text.fontSize = 26; text.fontSizeMin = 18; text.fontSizeMax = 26;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.margin = Vector4.zero;
        }

        private void RestoreFocusedItems()
        {
            foreach (var snapshot in focused) snapshot.Restore();
            focused.Clear();
            if (focusCanvas != null) focusCanvas.gameObject.SetActive(false);
        }

        // This snapshot is taken at click time, after the last drag/drop, never from a saved origin.
        private sealed class FocusSnapshot
        {
            public readonly RectTransform Rect;
            private readonly RectState[] rects;
            private readonly TextState[] texts;
            private readonly Vector3 worldPosition, worldScale;
            private readonly Quaternion worldRotation;
            private readonly IdentityDocumentView identity;
            private readonly OrderDocumentView order;
            private readonly bool expanded;
            private Vector3 destination, destinationScale;
            private Quaternion destinationRotation;
            private Vector2 destinationSize;

            public FocusSnapshot(RectTransform rect)
            {
                Rect = rect;
                worldPosition = rect.position; worldRotation = rect.rotation; worldScale = rect.lossyScale;
                var children = rect.GetComponentsInChildren<RectTransform>(true);
                rects = new RectState[children.Length];
                for (int i = 0; i < children.Length; i++) rects[i] = new RectState(children[i]);
                var labels = rect.GetComponentsInChildren<TMP_Text>(true);
                texts = new TextState[labels.Length];
                for (int i = 0; i < labels.Length; i++) texts[i] = new TextState(labels[i]);
                identity = rect.GetComponent<IdentityDocumentView>(); order = rect.GetComponent<OrderDocumentView>();
                expanded = identity != null ? identity.expanded : order != null && order.expanded;
            }

            public void Prepare(RectTransform parent, Vector2 position, Vector2 size, float scale)
            {
                Rect.SetParent(parent, true);
                Rect.anchorMin = Rect.anchorMax = Rect.pivot = Vector2.one * .5f;
                Rect.position = worldPosition;
                destination = parent.TransformPoint(position);
                destinationRotation = parent.rotation;
                destinationScale = Vector3.one * scale;
                destinationSize = size;
            }

            public void Animate(Sequence sequence, bool opening, float duration)
            {
                Vector3 returnScale = new Vector3(worldScale.x / Rect.parent.lossyScale.x,
                    worldScale.y / Rect.parent.lossyScale.y, worldScale.z / Rect.parent.lossyScale.z);
                sequence.Join(Rect.DOMove(opening ? destination : worldPosition, duration));
                sequence.Join(Rect.DORotateQuaternion(opening ? destinationRotation : worldRotation, duration));
                sequence.Join(Rect.DOScale(opening ? destinationScale : returnScale, duration));
                sequence.Join(Rect.DOSizeDelta(opening ? destinationSize : rects[0].Size, duration));
            }

            public void Restore()
            {
                if (Rect == null) return;
                if (identity != null) identity.SetExpanded(expanded);
                if (order != null) order.expanded = expanded;
                foreach (var saved in rects) saved.Restore();
                foreach (var saved in texts) saved.Restore();
            }
        }

        private sealed class RectState
        {
            private readonly RectTransform rect;
            private readonly Transform parent;
            private readonly int sibling;
            private readonly Vector2 min, max, pivot;
            public readonly Vector2 Size;
            private readonly Vector3 position, scale;
            private readonly Quaternion rotation;
            public RectState(RectTransform value)
            {
                rect = value; parent = value.parent; sibling = value.GetSiblingIndex();
                min = value.anchorMin; max = value.anchorMax; pivot = value.pivot; Size = value.sizeDelta;
                position = value.anchoredPosition3D; scale = value.localScale; rotation = value.localRotation;
            }
            public void Restore()
            {
                if (rect == null) return;
                rect.SetParent(parent, false); rect.SetSiblingIndex(sibling);
                rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.sizeDelta = Size;
                rect.anchoredPosition3D = position; rect.localRotation = rotation; rect.localScale = scale;
            }
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

        private void Unwire()
        {
            dialogueButton.onClick.RemoveListener(Advance); inspectButton.onClick.RemoveListener(Open);
            if (dummyCloseButton != null && dummyCloseButton != closeButton) dummyCloseButton.onClick.RemoveListener(Close);
            closeButton.onClick.RemoveListener(Close); passButton.onClick.RemoveListener(Pass);
            nonPassButton.onClick.RemoveListener(NonPass); restartButton.onClick.RemoveListener(RestartFlow);
            deskButton.onClick.RemoveListener(FocusDesk);
            frontButton.onClick.RemoveListener(FocusFront);
        }
        private void ClearNpcData()
        {
            npcName.text = string.Empty;
            if (identityDocument != null) identityDocument.Clear();
            if (expandedIdentityDocument != null) expandedIdentityDocument.Clear();
            if (orderDocument != null) orderDocument.Clear();
            if (expandedOrderDocument != null) expandedOrderDocument.Clear();
            portrait.color = Color.clear;
            resultStamp.text = string.Empty; resultStamp.gameObject.SetActive(false);
            dialogueWriter.ShowText(string.Empty);
        }
        private void OnDisable()
        {
            // The controller hosts presentation enumerators, so disabling only this component
            // must cancel that host too before it can issue the next NPC/reaction callback.
            if (flow != null) flow.StopAllCoroutines();
            StopAllCoroutines();
            CancelActivity();
        }
        private void OnDestroy() { CancelActivity(); if (initialized) Unwire(); }
    }
}
