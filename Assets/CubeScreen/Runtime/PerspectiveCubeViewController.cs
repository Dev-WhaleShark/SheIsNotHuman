using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// 단일 Perspective 카메라를 육면체의 각 면으로 회전한다.
    /// </summary>
    public sealed class PerspectiveCubeViewController : MonoBehaviour
    {
        [Header("화면 전환")]
        [SerializeField, Min(0.05f)] private float turnDuration = 0.4f;
        [SerializeField, Min(1f)] private float dragThreshold = 80f;
        [SerializeField] private Ease turnEase = Ease.InOutSine;

        [Header("카메라")]
        [SerializeField] private Camera viewCamera;
        [SerializeField, Range(45f, 100f)] private float horizontalFieldOfView = 80f;
        [SerializeField, Range(45f, 120f)] private float verticalFieldOfView = 90f;
        [SerializeField] private Volume lensVolume;
        [SerializeField, Min(0f)] private float bottomForwardOffset = 2.625f;

        private Sequence _turnSequence;
        private Quaternion _targetRotation;
        private Quaternion _frontRotation;
        private Vector3 _frontPosition;
        private Vector3 _targetPosition;
        private float _targetFieldOfView;
        private float _targetLensWeight;
        private float _normalLensWeight;
        private int _horizontalIndex;

#if ENABLE_INPUT_SYSTEM
        private Vector2 _dragStart;
        private bool _isDragging;
#endif

        public CubeFace CurrentFace { get; private set; } = CubeFace.Front;
        public bool IsTurning => _turnSequence != null;
        internal event System.Action FaceInputStateChanged;
        public Camera ViewCamera => viewCamera;
        public bool CanTurnLeft => isActiveAndEnabled && !InputBlocked && !IsTurning && IsHorizontalFace(CurrentFace);
        public bool CanTurnRight => CanTurnLeft;
        public bool CanTurnUp => isActiveAndEnabled && !InputBlocked && !IsTurning
            && (CurrentFace == CubeFace.Front || CurrentFace == CubeFace.Bottom);
        public bool CanTurnDown => isActiveAndEnabled && !InputBlocked && !IsTurning
            && (CurrentFace == CubeFace.Front || CurrentFace == CubeFace.Top);
        private bool _inputBlocked;

        [Sirenix.OdinInspector.ShowInInspector, Sirenix.OdinInspector.ReadOnly]
        public bool InputBlocked
        {
            get => _inputBlocked;
            set
            {
                _inputBlocked = value;
#if ENABLE_INPUT_SYSTEM
                if (value) _isDragging = false;
#endif
            }
        }

        /// <summary>Inspection flow can focus its two faces while player navigation is locked.</summary>
        public void FocusFace(CubeFace face, bool animate)
        {
            if (!isActiveAndEnabled || viewCamera == null ||
                (face != CubeFace.Front && face != CubeFace.Bottom)) return;

            _horizontalIndex = 0;
            CurrentFace = face;
            BeginTurn(face == CubeFace.Bottom
                ? _frontRotation * Quaternion.Euler(90f, 0f, 0f)
                : _frontRotation, face == CubeFace.Bottom ? verticalFieldOfView : horizontalFieldOfView);
            if (!animate) _turnSequence?.Complete(true);
        }

        private void Awake()
        {
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>();
            if (viewCamera == null)
            {
                Debug.LogError("뷰 전환에 사용할 카메라가 없습니다.", this);
                enabled = false;
                return;
            }
            _frontRotation = transform.localRotation;
            _frontPosition = transform.localPosition;
            _targetPosition = _frontPosition;
            _targetRotation = _frontRotation;
            _targetFieldOfView = horizontalFieldOfView;
            _normalLensWeight = lensVolume != null ? lensVolume.weight : 1f;
            _targetLensWeight = _normalLensWeight;
            ApplyFieldOfView(horizontalFieldOfView);
        }

        private void OnDisable()
        {
            FaceInputStateChanged?.Invoke();
#if ENABLE_INPUT_SYSTEM
            _isDragging = false;
#endif
            if (_turnSequence == null)
            {
                return;
            }

            _turnSequence.Kill();
            _turnSequence = null;
            transform.localRotation = _targetRotation;
            transform.localPosition = _targetPosition;
            ApplyFieldOfView(_targetFieldOfView);
            if (lensVolume != null) lensVolume.weight = _targetLensWeight;
        }

        private void Update()
        {
            if (InputBlocked) return;
#if ENABLE_INPUT_SYSTEM
            ReadKeyboard();
            ReadMouseDrag();
#endif
        }

        public void TurnLeft()
        {
            if (!CanTurnLeft)
            {
                return;
            }

            _horizontalIndex = (_horizontalIndex + 3) % 4;
            CurrentFace = GetHorizontalFace(_horizontalIndex);
            BeginTurn(_frontRotation * Quaternion.Euler(0f, _horizontalIndex * 90f, 0f), horizontalFieldOfView);
        }

        public void TurnRight()
        {
            if (!CanTurnRight)
            {
                return;
            }

            _horizontalIndex = (_horizontalIndex + 1) % 4;
            CurrentFace = GetHorizontalFace(_horizontalIndex);
            BeginTurn(_frontRotation * Quaternion.Euler(0f, _horizontalIndex * 90f, 0f), horizontalFieldOfView);
        }

        public void TurnUp()
        {
            if (!CanTurnUp)
            {
                return;
            }

            if (CurrentFace == CubeFace.Front)
            {
                CurrentFace = CubeFace.Top;
                BeginTurn(_frontRotation * Quaternion.Euler(-90f, 0f, 0f), verticalFieldOfView);
            }
            else if (CurrentFace == CubeFace.Bottom)
            {
                ReturnToFront();
            }
        }

        public void TurnDown()
        {
            if (!CanTurnDown)
            {
                return;
            }

            if (CurrentFace == CubeFace.Front)
            {
                CurrentFace = CubeFace.Bottom;
                BeginTurn(_frontRotation * Quaternion.Euler(90f, 0f, 0f), verticalFieldOfView);
            }
            else if (CurrentFace == CubeFace.Top)
            {
                ReturnToFront();
            }
        }

        private void ReturnToFront()
        {
            _horizontalIndex = 0;
            CurrentFace = CubeFace.Front;
            BeginTurn(_frontRotation, horizontalFieldOfView);
        }

        private void BeginTurn(Quaternion destination, float destinationFieldOfView)
        {
#if ENABLE_INPUT_SYSTEM
            _isDragging = false;
#endif
            _targetRotation = destination;
            // Front에 붙인 Bottom의 중심으로 이동하고, 복귀 시 원위치로 돌아간다.
            _targetPosition = _frontPosition + (CurrentFace == CubeFace.Bottom
                ? _frontRotation * Vector3.forward * bottomForwardOffset
                : Vector3.zero);
            _targetFieldOfView = destinationFieldOfView;
            // Bottom은 같은 카메라에서 후처리만 꺼 평면 UI로 보여 준다.
            _targetLensWeight = CurrentFace == CubeFace.Bottom ? 0f : _normalLensWeight;
            _turnSequence?.Kill();
            _turnSequence = DOTween.Sequence()
                .SetUpdate(true)
                .Join(transform.DOLocalRotateQuaternion(destination, turnDuration).SetEase(turnEase))
                .Join(transform.DOLocalMove(_targetPosition, turnDuration).SetEase(turnEase))
                .Join(viewCamera.DOFieldOfView(destinationFieldOfView, turnDuration).SetEase(turnEase));
            if (lensVolume != null)
                _turnSequence.Join(DOTween.To(() => lensVolume.weight, value => lensVolume.weight = value,
                    _targetLensWeight, turnDuration).SetEase(turnEase));
            _turnSequence
                .OnComplete(() =>
                {
                    transform.localRotation = destination;
                    transform.localPosition = _targetPosition;
                    ApplyFieldOfView(destinationFieldOfView);
                    _turnSequence = null;
                    FaceInputStateChanged?.Invoke();
                });
            // Synchronize keyboard/submit gates immediately, even inside an EventSystem callback.
            FaceInputStateChanged?.Invoke();
        }

        private void ApplyFieldOfView(float fieldOfView)
        {
            if (viewCamera != null)
            {
                viewCamera.fieldOfView = fieldOfView;
            }
        }

        private static bool IsHorizontalFace(CubeFace face)
        {
            return face is CubeFace.Front or CubeFace.Right or CubeFace.Back or CubeFace.Left;
        }

        private static CubeFace GetHorizontalFace(int index)
        {
            return index switch
            {
                1 => CubeFace.Right,
                2 => CubeFace.Back,
                3 => CubeFace.Left,
                _ => CubeFace.Front
            };
        }

#if ENABLE_INPUT_SYSTEM
        private void ReadKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || IsTurning)
            {
                return;
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            {
                TurnLeft();
            }
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            {
                TurnRight();
            }
            else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            {
                TurnUp();
            }
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            {
                TurnDown();
            }
        }

        private void ReadMouseDrag()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame && !IsTurning)
            {
                _dragStart = mouse.position.ReadValue();
                _isDragging = true;
            }

            if (!mouse.rightButton.wasReleasedThisFrame || !_isDragging)
            {
                return;
            }

            _isDragging = false;
            Vector2 delta = mouse.position.ReadValue() - _dragStart;
            if (IsTurning || delta.magnitude < dragThreshold)
            {
                return;
            }

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                if (delta.x < 0f)
                {
                    TurnRight();
                }
                else
                {
                    TurnLeft();
                }
            }
            else if (delta.y < 0f)
            {
                TurnUp();
            }
            else
            {
                TurnDown();
            }
        }
#endif

        private void OnValidate()
        {
            turnDuration = Mathf.Max(0.05f, turnDuration);
            dragThreshold = Mathf.Max(1f, dragThreshold);
            bottomForwardOffset = Mathf.Max(0f, bottomForwardOffset);
        }
    }
}
