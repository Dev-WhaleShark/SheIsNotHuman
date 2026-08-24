using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// Rotates the viewer between the six faces of the screen room.
    /// The same public methods can be connected to uGUI buttons.
    /// </summary>
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

        [SerializeField, Min(0.05f)] private float turnDuration = 0.45f;
        [SerializeField, Min(1f)] private float dragThreshold = 80f;
        [SerializeField, Range(60f, 130f)] private float frontBackFieldOfView = 95f;
        [SerializeField, Range(45f, 100f)] private float sideFieldOfView = 63f;
        [FormerlySerializedAs("verticalFieldOfView")]
        [SerializeField, Range(60f, 130f)] private float topFieldOfView = 95f;
        [SerializeField, Range(45f, 100f)] private float bottomFieldOfView = 90.1f;
        [SerializeField] private Ease turnEase = Ease.InOutSine;

        private Sequence _turnSequence;
        private Quaternion _targetRotation;
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

        public bool IsTurning => _turnSequence != null;
        public bool CanTurnLeft => !IsTurning && _verticalView == VerticalView.Horizontal;
        public bool CanTurnRight => !IsTurning && _verticalView == VerticalView.Horizontal;
        public bool CanTurnUp => !IsTurning
            && ((_verticalView == VerticalView.Horizontal && _horizontalView == HorizontalView.Front)
                || _verticalView == VerticalView.Bottom);
        public bool CanTurnDown => !IsTurning
            && ((_verticalView == VerticalView.Horizontal && _horizontalView == HorizontalView.Front)
                || _verticalView == VerticalView.Top);

        private void Awake()
        {
            _targetRotation = transform.rotation;
            _horizontalRotation = _targetRotation;
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

            _turnSequence.Kill();
            _turnSequence = null;
            transform.rotation = _targetRotation;
            ApplyFieldOfView(_targetFieldOfView);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            ReadKeyboard();
            ReadMouseDrag();
#endif
        }

        public void TurnLeft()
        {
            TurnHorizontal(-90f);
        }

        public void TurnRight()
        {
            TurnHorizontal(90f);
        }

        public void TurnUp()
        {
            if (IsTurning)
            {
                return;
            }

            if (_verticalView == VerticalView.Horizontal && _horizontalView == HorizontalView.Front)
            {
                _horizontalRotation = _targetRotation;
                _verticalView = VerticalView.Top;
                BeginTurn(CreateVerticalDestination(-90f), topFieldOfView);
            }
            else if (_verticalView == VerticalView.Bottom)
            {
                _verticalView = VerticalView.Horizontal;
                BeginTurn(_horizontalRotation, GetHorizontalFieldOfView(_horizontalView));
            }
        }

        public void TurnDown()
        {
            if (IsTurning)
            {
                return;
            }

            if (_verticalView == VerticalView.Horizontal && _horizontalView == HorizontalView.Front)
            {
                _horizontalRotation = _targetRotation;
                _verticalView = VerticalView.Bottom;
                BeginTurn(CreateVerticalDestination(90f), bottomFieldOfView);
            }
            else if (_verticalView == VerticalView.Top)
            {
                _verticalView = VerticalView.Horizontal;
                BeginTurn(_horizontalRotation, GetHorizontalFieldOfView(_horizontalView));
            }
        }

        private void TurnHorizontal(float angle)
        {
            if (IsTurning || _verticalView != VerticalView.Horizontal)
            {
                return;
            }

            Vector3 worldAxis = _targetRotation * Vector3.up;
            Quaternion destination = Quaternion.AngleAxis(angle, worldAxis) * _targetRotation;
            int direction = angle > 0f ? 1 : -1;
            _horizontalView = (HorizontalView)(((int)_horizontalView + direction + 4) % 4);
            _horizontalRotation = destination;
            BeginTurn(destination, GetHorizontalFieldOfView(_horizontalView));
        }

        private Quaternion CreateVerticalDestination(float angle)
        {
            Vector3 worldAxis = _horizontalRotation * Vector3.right;
            return Quaternion.AngleAxis(angle, worldAxis) * _horizontalRotation;
        }

        private void BeginTurn(Quaternion destination, float destinationFieldOfView)
        {
            _targetRotation = destination;
            _targetFieldOfView = destinationFieldOfView;
            float animatedFieldOfView = _viewerCamera != null
                ? _viewerCamera.fieldOfView
                : destinationFieldOfView;

            _turnSequence?.Kill();
            _turnSequence = DOTween.Sequence()
                .SetUpdate(true)
                .Join(transform.DORotateQuaternion(destination, turnDuration).SetEase(turnEase))
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
                    transform.rotation = destination;
                    ApplyFieldOfView(destinationFieldOfView);
                    _turnSequence = null;
                });
        }

        private void ApplyFieldOfView(float fieldOfView)
        {
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
            frontBackFieldOfView = Mathf.Clamp(frontBackFieldOfView, 60f, 130f);
            sideFieldOfView = Mathf.Clamp(sideFieldOfView, 45f, 100f);
            topFieldOfView = Mathf.Clamp(topFieldOfView, 60f, 130f);
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
