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
    /// <summary>검수 화면의 입력 연결과 연출을 담당한다. 게임 진행·판정은 InspectionFlowController에 요청한다.</summary>
    public sealed class InspectionMvpView : InspectionPresentation
    {
        [Title("Scene wiring"), Required] public PerspectiveCubeViewController navigation;
        [Required] public RectTransform npcRoot, documentsRoot, modalPanel;
        [Required] public CanvasGroup npcGroup, documentsGroup, modalGroup;
        [Required] public GameObject modalShield;
        [Required] public UnityEngine.UI.Image portrait, identityPortrait;
        [Required] public TMP_Text npcName, hint, stateLabel, identitySummary, orderSummary, identityDetail, orderDetail, resultStamp;
        [Required] public TypewriterComponent dialogueWriter;
        [Required] public UnityEngine.UI.Button dialogueButton, identityButton, orderButton, closeButton, passButton, nonPassButton, restartButton;
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
        // 취소된 코루틴이 yield 이후 재개되어 새 화면을 덮어쓰지 못하도록 실행 세대를 비교한다.
        private int activityVersion;
        // 대사만 새로 표시한 경우에도 이전 숨김 연출이 새 대사를 꺼 버리지 못하게 별도로 구분한다.
        private int dialogueVersion;

        /// <summary>입력 콜백을 현재 컨트롤러에 연결한다. 재시작마다 리스너가 쌓이지 않도록 기존 연결을 해제한다.</summary>
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
            closeButton.onClick.AddListener(Close);
            if (dummyCloseButton != null && dummyCloseButton != closeButton) dummyCloseButton.onClick.AddListener(Close);
            passButton.onClick.AddListener(Pass);
            nonPassButton.onClick.AddListener(NonPass);
            restartButton.onClick.AddListener(RestartFlow);
            initialized = true;
            ResetPresentation();
        }

        // 버튼의 interactable 표시와 실제 콜백 모두에서 검사해 회전·연출 중 들어온 요청도 막는다.
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

        /// <summary>현재 면·상태·포인터 점유를 기준으로 물품 입력을 허용한다. 소품은 대화/완료 단계에도 열 수 있다.</summary>
        public bool CanInteractWith(DeskInspectableItem item) => item != null && initialized && isActiveAndEnabled && Stable && !modalOpen
            && (navigation == null || (navigation.isActiveAndEnabled && navigation.CurrentFace == CubeFace.Bottom && !navigation.IsTurning))
            && (DeskInspectableItem.ActiveItem == null || DeskInspectableItem.ActiveItem == item)
            && (item.kind == DeskItemKind.Document ? state == InspectionState.Inspecting
                : state == InspectionState.Dialogue || state == InspectionState.Inspecting || state == InspectionState.Completed);

        /// <summary>문서는 컨트롤러의 검사 흐름으로, 소품은 판정 없는 자체 확대 흐름으로 전달한다.</summary>
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
        /// <summary>물품이 포인터를 점유하거나 놓은 즉시 버튼과 화면 회전의 잠금을 갱신한다.</summary>
        public void RefreshDeskInput() { if (initialized) RefreshInput(); }

        private void Update() { if (initialized) RefreshInput(); }
        // 상태별 입력 허용을 한곳에서 갱신한다. 모달뿐 아니라 서류 이동·물품 누르기도 화면 회전을 잠근다.
        private void RefreshInput()
        {
            bool stable = Stable && !DeskInspectableItem.AnyPointerInteraction && OnFace(CubeFace.Bottom);
            bool inspecting = state == InspectionState.Inspecting;
            dialogueButton.interactable = stable && !modalOpen && state == InspectionState.Dialogue;
            identityButton.interactable = orderButton.interactable = stable && !modalOpen && inspecting;
            closeButton.interactable = stable && modalOpen && (inspecting || dummyMode);
            if (dummyCloseButton != null && dummyCloseButton != closeButton) dummyCloseButton.interactable = stable && modalOpen && dummyMode;
            passButton.interactable = nonPassButton.interactable = stable && modalOpen && inspecting && !dummyMode;
            restartButton.interactable = stable && !modalOpen;
            if (navigation != null)
                navigation.InputBlocked = modalOpen || modalBusy || motionBusy || DeskInspectableItem.AnyPointerInteraction
                    || (state != InspectionState.Dialogue && state != InspectionState.Inspecting && state != InspectionState.Completed);
        }

        /// <summary>연출과 확대를 취소한 뒤 원래 위치·투명도·문서 표시를 새 실행의 시작 상태로 돌린다.</summary>
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

        /// <summary>확대 원본을 먼저 복원한 뒤 다음 방문자의 서류를 연결하고 기본 책상 위치를 적용한다.</summary>
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

        /// <summary>숨김 연출을 무효화하고 Text Animator의 타이핑으로 새 문장을 표시한다.</summary>
        public override void ShowDialogue(string text)
        {
            RestoreDialogue();
            dialogueWriter.StopShowingText();
            dialogueWriter.SetTypewriterSpeed(typingSpeed);
            dialogueWriter.ShowText(text ?? string.Empty);
        }
        /// <summary>Text Animator의 현재 타이핑만 건너뛴다. 대사 인덱스는 컨트롤러가 관리한다.</summary>
        public override void CompleteDialogue() => dialogueWriter.SkipTypewriter();
        /// <summary>현재 행동 안내를 즉시 갱신한다.</summary>
        public override void SetHint(string text) => hint.text = text;
        /// <summary>진행 상태를 받아 입력을 갱신한다. 단계가 바뀌면 진행 중인 물품 제스처와 소품 확대를 취소한다.</summary>
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

        /// <summary>정면 입장 연출 후 책상으로 시점을 이동한다. 대기 지점마다 취소 여부를 확인한다.</summary>
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

        /// <summary>대사를 숨기고 책상에 서류를 전달한 뒤 문서 레이캐스트를 허용한다.</summary>
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

        /// <summary>문서나 소품 원본을 확대 영역으로 이동하고 닫을 때 복원한다. 전환 전체에서 배경 입력을 차단한다.</summary>
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

        /// <summary>확대 원본을 책상으로 복귀시킨 뒤 서류를 회수하고 확정된 판정을 표시한다.</summary>
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
            // 컨트롤러가 NPC 반응을 표시하는 동안에도 판정 결과는 책상에 남긴다.
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

        /// <summary>대사를 숨기고 정면에서 퇴장시킨 뒤 이전 방문자의 표시 데이터를 비운다.</summary>
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

        /// <summary>남은 연출을 정리하고 책상에 완료 안내와 재시작 버튼을 표시한다.</summary>
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

        // 일시정지 배율과 무관하게 UI 연출을 진행하고 GameObject 수명에 연결한다. 취소 시 완료 콜백은 실행하지 않는다.
        private IEnumerator Play(Tween tween)
        {
            int version = activityVersion;
            activeTween = tween.SetUpdate(true).SetLink(gameObject);
            yield return activeTween.WaitForCompletion();
            if (version == activityVersion) activeTween = null;
        }
        /// <summary>기존 실행 세대를 폐기하고 트윈·타이핑·입력 점유를 정리한다. 확대된 동일 원본도 반드시 복원한다.</summary>
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
            // 복제본을 만들지 않는다. 한 원본을 이동하면 형제 순서가 바뀌므로 모든 원본을 먼저 기록한다.
            if (dummyMode) focused.Add(new FocusSnapshot((RectTransform)selectedDummy.transform));
            else
            {
                focused.Add(new FocusSnapshot((RectTransform)identityButton.transform));
                focused.Add(new FocusSnapshot((RectTransform)orderButton.transform));
            }
            var camera = navigation.ViewCamera;
            // 기존 원본보다 카메라에 가까운 평면을 쓰되 near clip 바깥에 두어 확대 중 잘림을 피한다.
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
                Vector2 size = dummyMode ? focused[i].Rect.rect.size :
                    i == 0 ? new Vector2(460, 280) : new Vector2(310, 480);
                Vector2 position = dummyMode ? new Vector2(0, 20) :
                    i == 0 ? new Vector2(-230, 30) : new Vector2(235, 45);
                float itemScale = dummyMode ? Mathf.Min(720f / Mathf.Max(1, size.x), 330f / Mathf.Max(1, size.y)) : 1;
                focused[i].Prepare(focusItemsRoot, position, size, itemScale);
            }
            if (!dummyMode)
            {
                identityDocument.SetExpanded(true);
                orderDocument.SetExpanded(true);
            }
            focusControls.SetAsLastSibling();
        }

        // 닫기뿐 아니라 취소·NPC 교체 경로도 같은 복원을 사용해 원본이 확대 계층에 남지 않게 한다.
        private void RestoreFocusedItems()
        {
            foreach (var snapshot in focused) snapshot.Restore();
            focused.Clear();
            if (focusCanvas != null) focusCanvas.gameObject.SetActive(false);
        }

        // 최초 저장 위치가 아닌 클릭 직전 상태를 기록한다. 사용자가 옮긴 위치와 형제 순서를 그대로 복원해야 한다.
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

            // 같은 RectTransform을 재부모화하므로 문서 데이터와 컴포넌트의 동일성이 유지된다.
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

            // 확대 캔버스의 배율이 달라도 닫기 연출 끝의 월드 크기가 클릭 시점과 같아야 한다.
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
                if (order != null) order.SetExpanded(expanded);
                foreach (var saved in rects) saved.Restore();
                foreach (var saved in texts) saved.Restore();
            }
        }

        // 원본 및 자식의 계층·레이아웃을 함께 저장해 확대용 서식이 책상 배치에 남지 않게 한다.
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

        // 확대 중 바꾼 줄 배치·글자 크기·본문을 모두 복원한다. 데이터 에셋을 다시 만들 필요가 없다.
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
            dialogueButton.onClick.RemoveListener(Advance);
            if (dummyCloseButton != null && dummyCloseButton != closeButton) dummyCloseButton.onClick.RemoveListener(Close);
            closeButton.onClick.RemoveListener(Close); passButton.onClick.RemoveListener(Pass);
            nonPassButton.onClick.RemoveListener(NonPass); restartButton.onClick.RemoveListener(RestartFlow);
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
            // 표현 열거자는 컨트롤러에서 실행되므로 뷰만 비활성화해도 호스트를 멈춰야 한다.
            // 그렇지 않으면 취소 후에도 다음 NPC나 반응 콜백이 다시 화면을 갱신할 수 있다.
            if (flow != null) flow.StopAllCoroutines();
            StopAllCoroutines();
            CancelActivity();
        }
        private void OnDestroy() { CancelActivity(); if (initialized) Unwire(); }
    }
}
