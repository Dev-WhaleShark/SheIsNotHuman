using DG.Tweening;
using UnityEngine;

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
        [SerializeField, Range(45f, 100f)] private float horizontalFieldOfView = 65f;
        [SerializeField, Range(45f, 120f)] private float verticalFieldOfView = 90f;

        private Sequence _turnSequence;
        private Quaternion _targetRotation;
        private int _horizontalIndex;

#if ENABLE_INPUT_SYSTEM
        private Vector2 _dragStart;
        private bool _isDragging;
#endif

        public CubeFace CurrentFace { get; private set; } = CubeFace.Front;
        public bool IsTurning => _turnSequence != null;

        private void Awake()
        {
            viewCamera ??= GetComponentInChildren<Camera>();
            _targetRotation = transform.localRotation;
            ApplyFieldOfView(horizontalFieldOfView);
        }

        private void OnDisable()
        {
            if (_turnSequence == null)
            {
                return;
            }

            _turnSequence.Kill();
            _turnSequence = null;
            transform.localRotation = _targetRotation;
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
            if (IsTurning || !IsHorizontalFace(CurrentFace))
            {
                return;
            }

            _horizontalIndex = (_horizontalIndex + 3) % 4;
            CurrentFace = GetHorizontalFace(_horizontalIndex);
            BeginTurn(Quaternion.Euler(0f, _horizontalIndex * 90f, 0f), horizontalFieldOfView);
        }

        public void TurnRight()
        {
            if (IsTurning || !IsHorizontalFace(CurrentFace))
            {
                return;
            }

            _horizontalIndex = (_horizontalIndex + 1) % 4;
            CurrentFace = GetHorizontalFace(_horizontalIndex);
            BeginTurn(Quaternion.Euler(0f, _horizontalIndex * 90f, 0f), horizontalFieldOfView);
        }

        public void TurnUp()
        {
            if (IsTurning)
            {
                return;
            }

            if (CurrentFace == CubeFace.Front)
            {
                CurrentFace = CubeFace.Top;
                BeginTurn(Quaternion.Euler(-90f, 0f, 0f), verticalFieldOfView);
            }
            else if (CurrentFace == CubeFace.Bottom)
            {
                ReturnToFront();
            }
        }

        public void TurnDown()
        {
            if (IsTurning)
            {
                return;
            }

            if (CurrentFace == CubeFace.Front)
            {
                CurrentFace = CubeFace.Bottom;
                BeginTurn(Quaternion.Euler(90f, 0f, 0f), verticalFieldOfView);
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
            BeginTurn(Quaternion.identity, horizontalFieldOfView);
        }

        private void BeginTurn(Quaternion destination, float destinationFieldOfView)
        {
            _targetRotation = destination;
            _turnSequence?.Kill();
            _turnSequence = DOTween.Sequence()
                .SetUpdate(true)
                .Join(transform.DOLocalRotateQuaternion(destination, turnDuration).SetEase(turnEase))
                .Join(viewCamera.DOFieldOfView(destinationFieldOfView, turnDuration).SetEase(turnEase))
                .OnComplete(() =>
                {
                    transform.localRotation = destination;
                    ApplyFieldOfView(destinationFieldOfView);
                    _turnSequence = null;
                });
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
        }
    }
}
