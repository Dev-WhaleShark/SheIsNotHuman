using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SheIsNotHuman.CubeScreen
{
    public enum CubeFace
    {
        Front,
        Right,
        Back,
        Left,
        Top,
        Bottom
    }

    /// <summary>
    /// 뷰어를 육면체의 각 화면으로 회전·이동한다.
    /// 공개 전환 메서드는 공통 UI 버튼에서도 호출한다.
    /// </summary>
    [HideMonoScript]
    public sealed class CubeScreenController : MonoBehaviour
    {
        private enum HorizontalView
        {
            Front,
            Right,
            Back,
            Left
        }

        private enum VerticalView
        {
            Horizontal,
            Top,
            Bottom
        }

        [TitleGroup("화면 전환")]
        [LabelText("회전 시간"), SuffixLabel("초")]
        [SerializeField, Min(0.05f)] private float turnDuration = 0.45f;

        [TitleGroup("화면 전환")]
        [LabelText("Bottom 전진 거리"), SuffixLabel("unit")]
        [SerializeField, Min(0f)] private float bottomForwardOffset = 3.5f;

        [TitleGroup("화면 전환")]
        [LabelText("드래그 인식 거리"), SuffixLabel("px")]
        [SerializeField, Min(1f)] private float dragThreshold = 80f;

        [TitleGroup("면별 카메라 화각")]
        [LabelText("Front / Back"), SuffixLabel("°")]
        [SerializeField, Range(45f, 100f)] private float frontBackFieldOfView = 63f;

        [TitleGroup("면별 카메라 화각")]
        [LabelText("Left / Right"), SuffixLabel("°")]
        [SerializeField, Range(45f, 100f)] private float sideFieldOfView = 63f;

        [FormerlySerializedAs("verticalFieldOfView")]
        [TitleGroup("면별 카메라 화각")]
        [LabelText("Top"), SuffixLabel("°")]
        [SerializeField, Range(90f, 130f)] private float topFieldOfView = 121.3f;

        [TitleGroup("면별 카메라 화각")]
        [LabelText("Bottom"), SuffixLabel("°")]
        [SerializeField, Range(45f, 100f)] private float bottomFieldOfView = 90.1f;

        [TitleGroup("화면 전환")]
        [LabelText("회전 Ease")]
        [SerializeField] private Ease turnEase = Ease.InOutSine;

        private Sequence _turnSequence;
        private Quaternion _targetRotation;

        private Vector3 _targetPosition;
        private Vector3 _horizontalPosition;
        private Quaternion _horizontalRotation;
        private HorizontalView _horizontalView;
        private VerticalView _verticalView;
        private Camera _viewerCamera;
        private Camera _eventCamera;
        private float _targetFieldOfView;

#if ENABLE_INPUT_SYSTEM
        private Vector2 _dragStart;
        private bool _isDragging;
#endif

        [ShowInInspector, ReadOnly, HideInEditorMode]
        [FoldoutGroup("런타임 상태"), LabelText("회전 중")]
        public bool IsTurning => _turnSequence != null;

        [ShowInInspector, ReadOnly, HideInEditorMode]
        [FoldoutGroup("런타임 상태"), LabelText("현재 면")]
        public CubeFace CurrentFace => _verticalView switch
        {
            VerticalView.Top => CubeFace.Top,
            VerticalView.Bottom => CubeFace.Bottom,
            _ => _horizontalView switch
            {
                HorizontalView.Front => CubeFace.Front,
                HorizontalView.Right => CubeFace.Right,
                HorizontalView.Back => CubeFace.Back,
                HorizontalView.Left => CubeFace.Left,
                _ => CubeFace.Front
            }
        };

        [ShowInInspector, ReadOnly, HideInEditorMode]
        [FoldoutGroup("런타임 상태"), LabelText("수평 화면")]
        private HorizontalView CurrentHorizontalView => _horizontalView;

        [ShowInInspector, ReadOnly, HideInEditorMode]
        [FoldoutGroup("런타임 상태"), LabelText("수직 화면")]
        private VerticalView CurrentVerticalView => _verticalView;

        [ShowInInspector, ReadOnly, HideInEditorMode]
        [FoldoutGroup("런타임 상태"), LabelText("목표 FOV"), SuffixLabel("°")]
        private float CurrentTargetFieldOfView => _targetFieldOfView;

        // 현재 화면에서 가능한 방향만 공통 UI에 노출한다.
        public bool CanTurnLeft => !IsTurning && _verticalView == VerticalView.Horizontal;
        public bool CanTurnRight => !IsTurning && _verticalView == VerticalView.Horizontal;
        public bool CanTurnUp => !IsTurning
            && ((_verticalView == VerticalView.Horizontal && _horizontalView == HorizontalView.Front)
                || _verticalView == VerticalView.Bottom);
        public bool CanTurnDown => !IsTurning
            && ((_verticalView == VerticalView.Horizontal && _horizontalView == HorizontalView.Front)
                || _verticalView == VerticalView.Top);

        /// <summary>전환이 끝난 현재 면에만 UI 입력을 허용한다.</summary>
        public bool CanReceiveInput(CubeFace face)
        {
            return isActiveAndEnabled && !IsTurning && CurrentFace == face;
        }

        private void Awake()
        {
            // 씬 시작 자세를 정면 기준 상태로 저장한다.
            _targetRotation = transform.rotation;
            _targetPosition = transform.position;
            _horizontalRotation = _targetRotation;
            _horizontalPosition = _targetPosition;
            _horizontalView = HorizontalView.Front;
            _verticalView = VerticalView.Horizontal;
            _viewerCamera = Camera.main;
            _eventCamera = transform.Find("UIEventCamera")?.GetComponent<Camera>();
            _targetFieldOfView = GetHorizontalFieldOfView(_horizontalView);
            ApplyFieldOfView(_targetFieldOfView);
        }

        private void OnDisable()
        {
            if (_turnSequence == null)
            {
                return;
            }

            // 비활성화 중인 트윈을 정리하고 목표 자세로 고정한다.
            _turnSequence.Kill();
            _turnSequence = null;
            transform.SetPositionAndRotation(_targetPosition, _targetRotation);
            ApplyFieldOfView(_targetFieldOfView);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            ReadKeyboard();
            ReadMouseDrag();
#endif
        }

        /// <summary>현재 수평 화면에서 왼쪽 면으로 이동한다.</summary>
        [Button("← 왼쪽", ButtonSizes.Medium)]
        [ButtonGroup("플레이 테스트/수평"), DisableInEditorMode]
        public void TurnLeft()
        {
            TurnHorizontal(-90f);
        }

        /// <summary>현재 수평 화면에서 오른쪽 면으로 이동한다.</summary>
        [Button("오른쪽 →", ButtonSizes.Medium)]
        [ButtonGroup("플레이 테스트/수평"), DisableInEditorMode]
        public void TurnRight()
        {
            TurnHorizontal(90f);
        }

        /// <summary>정면에서 위를 보거나 Bottom에서 정면으로 돌아간다.</summary>
        [Button("↑ 위", ButtonSizes.Medium)]
        [ButtonGroup("플레이 테스트/수직"), DisableInEditorMode]
        public void TurnUp()
        {
            if (IsTurning)
            {
                return;
            }

            if (_verticalView == VerticalView.Horizontal && _horizontalView == HorizontalView.Front)
            {
                // Top에서 돌아올 수 있도록 정면 자세를 보관한다.
                _horizontalRotation = _targetRotation;
                _horizontalPosition = transform.position;
                _verticalView = VerticalView.Top;
                BeginTurn(CreateVerticalDestination(-90f), _horizontalPosition, topFieldOfView);
            }
            else if (_verticalView == VerticalView.Bottom)
            {
                _verticalView = VerticalView.Horizontal;
                BeginTurn(_horizontalRotation, _horizontalPosition, GetHorizontalFieldOfView(_horizontalView));
            }
        }

        /// <summary>정면에서 아래를 보거나 Top에서 정면으로 돌아간다.</summary>
        [Button("↓ 아래", ButtonSizes.Medium)]
        [ButtonGroup("플레이 테스트/수직"), DisableInEditorMode]
        public void TurnDown()
        {
            if (IsTurning)
            {
                return;
            }

            if (_verticalView == VerticalView.Horizontal && _horizontalView == HorizontalView.Front)
            {
                // Bottom을 가득 채우기 위해 회전과 함께 앞으로 이동한다.
                _horizontalRotation = _targetRotation;
                _horizontalPosition = transform.position;
                _verticalView = VerticalView.Bottom;
                Vector3 destinationPosition = _horizontalPosition
                    + (_horizontalRotation * Vector3.forward * bottomForwardOffset);
                BeginTurn(CreateVerticalDestination(90f), destinationPosition, bottomFieldOfView);
            }
            else if (_verticalView == VerticalView.Top)
            {
                _verticalView = VerticalView.Horizontal;
                BeginTurn(_horizontalRotation, _horizontalPosition, GetHorizontalFieldOfView(_horizontalView));
            }
        }

        private void TurnHorizontal(float angle)
        {
            // Top과 Bottom에서는 좌우 회전을 허용하지 않는다.
            if (IsTurning || _verticalView != VerticalView.Horizontal)
            {
                return;
            }

            Vector3 worldAxis = _targetRotation * Vector3.up;
            Quaternion destination = Quaternion.AngleAxis(angle, worldAxis) * _targetRotation;
            int direction = angle > 0f ? 1 : -1;
            _horizontalView = (HorizontalView)(((int)_horizontalView + direction + 4) % 4);
            _horizontalRotation = destination;
            _horizontalPosition = transform.position;
            BeginTurn(destination, _horizontalPosition, GetHorizontalFieldOfView(_horizontalView));
        }

        private Quaternion CreateVerticalDestination(float angle)
        {
            // 정면의 로컬 오른쪽 축을 기준으로 위아래를 회전한다.
            Vector3 worldAxis = _horizontalRotation * Vector3.right;
            return Quaternion.AngleAxis(angle, worldAxis) * _horizontalRotation;
        }

        private void BeginTurn(
            Quaternion destination,
            Vector3 destinationPosition,
            float destinationFieldOfView)
        {
            // 회전, 위치, 화각을 하나의 DOTween 시퀀스로 맞춘다.
            _targetRotation = destination;
            _targetPosition = destinationPosition;
            _targetFieldOfView = destinationFieldOfView;
            float animatedFieldOfView = _viewerCamera != null
                ? _viewerCamera.fieldOfView
                : destinationFieldOfView;

            _turnSequence?.Kill();
            _turnSequence = DOTween.Sequence()
                .SetUpdate(true)
                .Join(transform.DORotateQuaternion(destination, turnDuration).SetEase(turnEase))
                .Join(transform.DOMove(destinationPosition, turnDuration).SetEase(turnEase))
                .Join(DOTween.To(
                        () => animatedFieldOfView,
                        value =>
                        {
                            animatedFieldOfView = value;
                            ApplyFieldOfView(value);
                        },
                        destinationFieldOfView,
                        turnDuration)
                    .SetEase(turnEase))
                .OnComplete(() =>
                {
                    transform.SetPositionAndRotation(destinationPosition, destination);
                    ApplyFieldOfView(destinationFieldOfView);
                    _turnSequence = null;
                });
        }

        private void ApplyFieldOfView(float fieldOfView)
        {
            // 렌더 카메라와 UI 입력 카메라의 투영을 동일하게 유지한다.
            if (_viewerCamera != null)
            {
                _viewerCamera.fieldOfView = fieldOfView;
            }

            if (_eventCamera != null)
            {
                _eventCamera.fieldOfView = fieldOfView;
            }
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

            if (mouse.rightButton.wasPressedThisFrame)
            {
                _dragStart = mouse.position.ReadValue();
                _isDragging = true;
            }

            if (!mouse.rightButton.wasReleasedThisFrame || !_isDragging)
            {
                return;
            }

            _isDragging = false;

            if (IsTurning)
            {
                return;
            }

            Vector2 delta = mouse.position.ReadValue() - _dragStart;
            if (delta.magnitude < dragThreshold)
            {
                return;
            }

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                // 화면을 끌어당기는 방향과 시점 회전 방향은 반대다.
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
            frontBackFieldOfView = Mathf.Clamp(frontBackFieldOfView, 45f, 100f);
            sideFieldOfView = Mathf.Clamp(sideFieldOfView, 45f, 100f);
            topFieldOfView = Mathf.Clamp(topFieldOfView, 90f, 130f);
            bottomFieldOfView = Mathf.Clamp(bottomFieldOfView, 45f, 100f);
        }

        private float GetHorizontalFieldOfView(HorizontalView view)
        {
            return view == HorizontalView.Front || view == HorizontalView.Back
                ? frontBackFieldOfView
                : sideFieldOfView;
        }
    }
}
