using DG.Tweening;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// Reveals one shared set of navigation buttons when the pointer approaches
    /// a screen edge. Availability follows CubeScreenController's view rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CubeNavigationOverlay : MonoBehaviour
    {
        private enum Direction
        {
            Left,
            Right,
            Up,
            Down
        }

        [Header("References")]
        [SerializeField] private CubeScreenController controller;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private UnityEngine.UI.Button leftButton;
        [SerializeField] private UnityEngine.UI.Button rightButton;
        [SerializeField] private UnityEngine.UI.Button upButton;
        [SerializeField] private UnityEngine.UI.Button downButton;
        [SerializeField] private CanvasGroup leftGroup;
        [SerializeField] private CanvasGroup rightGroup;
        [SerializeField] private CanvasGroup upGroup;
        [SerializeField] private CanvasGroup downGroup;

        [Header("Edge Reveal")]
        [SerializeField, Min(1f)] private float edgeRevealDistance = 44f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;
        [SerializeField] private Ease fadeEase = Ease.OutQuad;

        private Canvas _canvas;
        private readonly bool[] _visible = new bool[4];

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            canvasRect ??= _canvas != null ? _canvas.transform as RectTransform : null;
        }

        private void OnEnable()
        {
            AddButtonListeners();
            SetImmediate(leftGroup, false);
            SetImmediate(rightGroup, false);
            SetImmediate(upGroup, false);
            SetImmediate(downGroup, false);
        }

        private void OnDisable()
        {
            RemoveButtonListeners();
            KillFade(leftGroup);
            KillFade(rightGroup);
            KillFade(upGroup);
            KillFade(downGroup);
        }

        private void Update()
        {
            if (controller == null || canvasRect == null || !TryGetPointerPosition(out Vector2 pointerPosition))
            {
                HideAll();
                return;
            }

            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    pointerPosition,
                    eventCamera,
                    out Vector2 localPoint))
            {
                HideAll();
                return;
            }

            Rect rect = canvasRect.rect;
            bool inside = rect.Contains(localPoint);
            SetVisible(Direction.Left, inside && controller.CanTurnLeft
                && localPoint.x <= rect.xMin + edgeRevealDistance);
            SetVisible(Direction.Right, inside && controller.CanTurnRight
                && localPoint.x >= rect.xMax - edgeRevealDistance);
            SetVisible(Direction.Up, inside && controller.CanTurnUp
                && localPoint.y >= rect.yMax - edgeRevealDistance);
            SetVisible(Direction.Down, inside && controller.CanTurnDown
                && localPoint.y <= rect.yMin + edgeRevealDistance);
        }

        private void AddButtonListeners()
        {
            if (controller == null)
            {
                return;
            }

            leftButton?.onClick.AddListener(controller.TurnLeft);
            rightButton?.onClick.AddListener(controller.TurnRight);
            upButton?.onClick.AddListener(controller.TurnUp);
            downButton?.onClick.AddListener(controller.TurnDown);
        }

        private void RemoveButtonListeners()
        {
            if (controller == null)
            {
                return;
            }

            leftButton?.onClick.RemoveListener(controller.TurnLeft);
            rightButton?.onClick.RemoveListener(controller.TurnRight);
            upButton?.onClick.RemoveListener(controller.TurnUp);
            downButton?.onClick.RemoveListener(controller.TurnDown);
        }

        private void SetVisible(Direction direction, bool visible)
        {
            int index = (int)direction;
            if (_visible[index] == visible)
            {
                return;
            }

            _visible[index] = visible;
            CanvasGroup group = GetGroup(direction);
            if (group == null)
            {
                return;
            }

            group.DOKill();
            group.interactable = visible;
            group.blocksRaycasts = visible;
            group.DOFade(visible ? 1f : 0f, fadeDuration)
                .SetEase(fadeEase)
                .SetUpdate(true);
        }

        private void HideAll()
        {
            SetVisible(Direction.Left, false);
            SetVisible(Direction.Right, false);
            SetVisible(Direction.Up, false);
            SetVisible(Direction.Down, false);
        }

        private CanvasGroup GetGroup(Direction direction)
        {
            return direction switch
            {
                Direction.Left => leftGroup,
                Direction.Right => rightGroup,
                Direction.Up => upGroup,
                Direction.Down => downGroup,
                _ => null
            };
        }

        private static void SetImmediate(CanvasGroup group, bool visible)
        {
            if (group == null)
            {
                return;
            }

            group.DOKill();
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private static void KillFade(CanvasGroup group)
        {
            group?.DOKill();
        }

        private static bool TryGetPointerPosition(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                position = default;
                return false;
            }

            position = mouse.position.ReadValue();
            return true;
#else
            position = Input.mousePosition;
            return true;
#endif
        }

        private void OnValidate()
        {
            edgeRevealDistance = Mathf.Max(1f, edgeRevealDistance);
            fadeDuration = Mathf.Max(0.01f, fadeDuration);
        }
    }
}
