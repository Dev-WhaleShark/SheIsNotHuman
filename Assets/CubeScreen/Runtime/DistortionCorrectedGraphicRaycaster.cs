using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SheIsNotHuman.CubeScreen
{
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

        // Navigation can be locked by a modal while its current-face controls remain usable.
        [BoxGroup("Face input"), ShowInInspector, ReadOnly]
        public bool IsFaceInputAllowed => !faceGateConfigured ||
            (faceController != null && faceController.isActiveAndEnabled &&
             !faceController.IsTurning && faceController.CurrentFace == ownerFace);

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
            // This group belongs only to this gate; other groups and their alpha are untouched.
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

        private void Update() => RefreshFaceInput();

        private void SubscribeController()
        {
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
                // Separate additive group: never borrow a modal/fade group's mutable state.
                _inputGroup = gameObject.AddComponent<CanvasGroup>();
                _inputGroup.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
            }
            bool allowed = IsFaceInputAllowed;
            _inputGroup.interactable = allowed;
            _inputGroup.blocksRaycasts = allowed;
            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (!allowed && selected != null && selected.transform.IsChildOf(transform))
                eventSystem.SetSelectedGameObject(null);
        }

        public override void Raycast(PointerEventData eventData, List<RaycastResult> resultAppendList)
        {
            if (!IsFaceInputAllowed) return;

            Vector2 physicalPosition = eventData.position;
            int firstResult = resultAppendList.Count;
            try
            {
                eventData.position = LensDistortionCoordinates.ScreenToUndistorted(eventCamera, physicalPosition);
                base.Raycast(eventData, resultAppendList);
                // Consumers and other raycasters continue to see the physical mouse position.
                for (int i = firstResult; i < resultAppendList.Count; i++)
                {
                    var hit = resultAppendList[i];
                    hit.screenPosition = physicalPosition;
                    resultAppendList[i] = hit;
                }
            }
            finally { eventData.position = physicalPosition; }
        }
    }
}
