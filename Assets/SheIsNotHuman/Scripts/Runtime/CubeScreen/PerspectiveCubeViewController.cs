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
    /// 정면의 로컬 자세를 기준으로 회전·이동·화각·렌즈 강도를 함께 전환한다.
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

        /// <summary>전환 시작 때 목표 면으로 바뀐다. 입력 가능 여부는 IsTurning도 함께 확인해야 한다.</summary>
        public CubeFace CurrentFace { get; private set; } = CubeFace.Front;
        /// <summary>시퀀스가 존재하는 동안 면 UI와 추가 사용자 탐색을 막는다.</summary>
        public bool IsTurning => _turnSequence != null;
        // 시작/완료/비활성화 시 입력 게이트가 같은 호출 안에서 상태를 갱신하도록 알린다.
        internal event System.Action FaceInputStateChanged;
        /// <summary>공통 내비게이션의 화면 픽셀 영역 판정에도 사용하는 표시 카메라다.</summary>
        public Camera ViewCamera => viewCamera;
        // 일반 탐색은 활성 상태·모달 잠금·전환 상태·현재 면의 이동 규칙을 모두 통과해야 한다.
        public bool CanTurnLeft => isActiveAndEnabled && !InputBlocked && !IsTurning && IsHorizontalFace(CurrentFace);
        public bool CanTurnRight => CanTurnLeft;
        public bool CanTurnUp => isActiveAndEnabled && !InputBlocked && !IsTurning
            && (CurrentFace == CubeFace.Front || CurrentFace == CubeFace.Bottom);
        public bool CanTurnDown => isActiveAndEnabled && !InputBlocked && !IsTurning
            && (CurrentFace == CubeFace.Front || CurrentFace == CubeFace.Top);
        private bool _inputBlocked;

        /// <summary>사용자 탐색만 잠근다. 잠금 시 진행 중 드래그를 취소하며 FocusFace는 계속 허용한다.</summary>
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

        /// <summary>
        /// 검사 흐름이 탐색 잠금 중에도 Front/Bottom으로 시점을 지정한다.
        /// animate가 false이면 완료 콜백까지 실행해 자세와 입력 상태를 즉시 확정한다.
        /// </summary>
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
            // 먼저 입력 게이트를 닫고, 트윈 취소 후에는 중간 자세가 아닌 저장된 목표로 정착한다.
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

        /// <summary>일반 탐색이 허용될 때 수평 네 면을 왼쪽으로 순환한다.</summary>
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

        /// <summary>일반 탐색이 허용될 때 수평 네 면을 오른쪽으로 순환한다.</summary>
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

        /// <summary>Front에서 Top으로 이동하거나 Bottom에서 정면 기준 자세로 복귀한다.</summary>
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

        /// <summary>Front에서 Bottom으로 이동하거나 Top에서 정면 기준 자세로 복귀한다.</summary>
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
            // 전환 전에 시작한 드래그가 전환 후 새 탐색으로 이어지지 않도록 무효화한다.
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
            // 강제 초점 이동은 기존 전환을 대체할 수 있다. 시퀀스는 항상 하나만 유지한다.
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
            // EventSystem 콜백 안에서 시작해도 키보드/제출 게이트를 즉시 닫는다.
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
