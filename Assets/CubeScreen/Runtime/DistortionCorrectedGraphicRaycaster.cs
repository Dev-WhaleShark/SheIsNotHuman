using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// 렌즈 왜곡으로 이동한 화면 좌표를 UI 투영 좌표로 되돌려 클릭 위치를 맞춘다.
    /// 면별 입력 게이트를 함께 관리해 회전 중이거나 숨겨진 면의 선택과 제출을 차단한다.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Canvas)), HideMonoScript, DefaultExecutionOrder(-1000)]
    public sealed class DistortionCorrectedGraphicRaycaster : UnityEngine.UI.GraphicRaycaster
    {
        [BoxGroup("Face input"), SerializeField, ReadOnly]
        private bool faceGateConfigured;

        [BoxGroup("Face input"), SerializeField, SceneObjectsOnly, ReadOnly]
        private PerspectiveCubeViewController faceController;

        [BoxGroup("Face input"), SerializeField, ReadOnly]
        private CubeFace ownerFace;

        private CanvasGroup _inputGroup;
        private PerspectiveCubeViewController _subscribedController;

        /// <summary>탐색 잠금과 별개로, 회전이 끝난 현재 면의 컨트롤은 모달에서도 사용할 수 있다.</summary>
        [BoxGroup("Face input"), ShowInInspector, ReadOnly]
        public bool IsFaceInputAllowed => !faceGateConfigured ||
            (faceController != null && faceController.isActiveAndEnabled &&
             !faceController.IsTurning && faceController.CurrentFace == ownerFace);

        /// <summary>소유 면을 연결하고, 활성 상태에서는 이전 컨트롤러 구독을 교체해 즉시 반영한다.</summary>
        public void ConfigureFaceGate(PerspectiveCubeViewController controller, CubeFace face)
        {
            faceController = controller;
            ownerFace = face;
            faceGateConfigured = true;
            if (isActiveAndEnabled)
            {
                SubscribeController();
                RefreshFaceInput();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SubscribeController();
            RefreshFaceInput();
        }

        protected override void OnDisable()
        {
            UnsubscribeController();
            // 이 게이트 전용 그룹만 해제한다. 다른 모달/페이드 그룹의 상태와 투명도는 보존한다.
            if (_inputGroup != null)
                _inputGroup.interactable = _inputGroup.blocksRaycasts = true;
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            UnsubscribeController();
            if (_inputGroup != null) Destroy(_inputGroup);
            base.OnDestroy();
        }

        // 이벤트 사이에 컨트롤러 활성 상태나 선택 객체가 바뀌어도 게이트를 다시 동기화한다.
        private void Update() => RefreshFaceInput();

        private void SubscribeController()
        {
            // 재설정/재활성화 시 이전 발행자부터 끊어 중복 구독과 참조 잔류를 막는다.
            UnsubscribeController();
            if (!Application.isPlaying || !faceGateConfigured || faceController == null) return;
            _subscribedController = faceController;
            _subscribedController.FaceInputStateChanged += RefreshFaceInput;
        }

        private void UnsubscribeController()
        {
            if (_subscribedController != null)
                _subscribedController.FaceInputStateChanged -= RefreshFaceInput;
            _subscribedController = null;
        }

        private void RefreshFaceInput()
        {
            if (!Application.isPlaying || !faceGateConfigured) return;
            if (_inputGroup == null)
            {
                // 기존 모달/페이드 그룹을 빌리지 않고 별도 그룹으로 입력 제한을 추가한다.
                _inputGroup = gameObject.AddComponent<CanvasGroup>();
                _inputGroup.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
            }
            bool allowed = IsFaceInputAllowed;
            _inputGroup.interactable = allowed;
            _inputGroup.blocksRaycasts = allowed;
            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            // 레이캐스트만 막으면 키보드 제출은 남을 수 있으므로 이 면 내부의 선택도 해제한다.
            if (!allowed && selected != null && selected.transform.IsChildOf(transform))
                eventSystem.SetSelectedGameObject(null);
        }

        /// <summary>UI 판정 동안만 포인터 좌표를 보정하고, 호출자에게는 실제 화면 좌표를 유지한다.</summary>
        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            if (!IsFaceInputAllowed) return;

            Vector2 physicalPosition = eventData.position;
            int firstResult = resultAppendList.Count;
            try
            {
                eventData.position = LensDistortionCoordinates.ScreenToUndistorted(eventCamera, physicalPosition);
                base.Raycast(eventData, resultAppendList);
                // 이번 호출이 추가한 결과만 복원해 다른 레이캐스터의 기존 결과를 보존한다.
                for (int i = firstResult; i < resultAppendList.Count; i++)
                {
                    var hit = resultAppendList[i];
                    hit.screenPosition = physicalPosition;
                    resultAppendList[i] = hit;
                }
            }
            // 예외가 나더라도 공유 PointerEventData에 보정 좌표를 남기지 않는다.
            finally { eventData.position = physicalPosition; }
        }
    }
}
