using System;
using DG.Tweening;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// 포인터가 화면 가장자리에 접근하면 공통 방향 버튼을 표시한다.
    /// 버튼 활성 여부는 컨트롤러의 현재 화면 규칙을 따른다.
    /// </summary>
    [DisallowMultipleComponent]
    [HideMonoScript]
    public sealed class CubeNavigationOverlay : MonoBehaviour
    {
        private enum Direction
        {
            Left,
            Right,
            Up,
            Down
        }

        [BoxGroup("필수 참조")]
        [Required, SceneObjectsOnly, LabelText("화면 컨트롤러")]
        [SerializeField] private CubeScreenController controller;

        [BoxGroup("필수 참조")]
        [Required, SceneObjectsOnly, LabelText("Canvas 영역")]
        [SerializeField] private RectTransform canvasRect;

        [BoxGroup("왼쪽 UI")]
        [Required, SceneObjectsOnly, LabelText("버튼")]
        [SerializeField] private Button leftButton;

        [BoxGroup("오른쪽 UI")]
        [Required, SceneObjectsOnly, LabelText("버튼")]
        [SerializeField] private Button rightButton;

        [BoxGroup("위쪽 UI")]
        [Required, SceneObjectsOnly, LabelText("버튼")]
        [SerializeField] private Button upButton;

        [BoxGroup("아래쪽 UI")]
        [Required, SceneObjectsOnly, LabelText("버튼")]
        [SerializeField] private Button downButton;

        [BoxGroup("왼쪽 UI")]
        [Required, SceneObjectsOnly, LabelText("페이드 그룹")]
        [SerializeField] private CanvasGroup leftGroup;

        [BoxGroup("오른쪽 UI")]
        [Required, SceneObjectsOnly, LabelText("페이드 그룹")]
        [SerializeField] private CanvasGroup rightGroup;

        [BoxGroup("위쪽 UI")]
        [Required, SceneObjectsOnly, LabelText("페이드 그룹")]
        [SerializeField] private CanvasGroup upGroup;

        [BoxGroup("아래쪽 UI")]
        [Required, SceneObjectsOnly, LabelText("페이드 그룹")]
        [SerializeField] private CanvasGroup downGroup;

        [BoxGroup("가장자리 표시")]
        [LabelText("감지 거리"), SuffixLabel("px")]
        [SerializeField, Min(1f)] private float edgeRevealDistance = 44f;

        [BoxGroup("가장자리 표시")]
        [LabelText("페이드 시간"), SuffixLabel("초")]
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;

        [BoxGroup("가장자리 표시")]
        [LabelText("페이드 Ease")]
        [SerializeField] private Ease fadeEase = Ease.OutQuad;

        private Canvas _canvas;
        private readonly bool[] _visible = new bool[4];
        private IDisposable _buttonSubscriptions;

        private void Awake()
        {
            // 포인터 좌표를 변환할 기준 Canvas를 한 번만 찾는다.
            _canvas = GetComponentInParent<Canvas>();
            canvasRect ??= _canvas != null ? _canvas.transform as RectTransform : null;
        }

        private void OnEnable()
        {
            SubscribeButtonClicks();
            SetImmediate(leftGroup, false);
            SetImmediate(rightGroup, false);
            SetImmediate(upGroup, false);
            SetImmediate(downGroup, false);
        }

        private void OnDisable()
        {
            _buttonSubscriptions?.Dispose();
            _buttonSubscriptions = null;
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

            // Overlay Canvas는 좌표 변환에 카메라가 필요하지 않다.
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

            // 포인터와 맞닿은 가장자리의 이동 가능한 버튼만 표시한다.
            SetVisible(Direction.Left, inside && controller.CanTurnLeft
                && localPoint.x <= rect.xMin + edgeRevealDistance);
            SetVisible(Direction.Right, inside && controller.CanTurnRight
                && localPoint.x >= rect.xMax - edgeRevealDistance);
            SetVisible(Direction.Up, inside && controller.CanTurnUp
                && localPoint.y >= rect.yMax - edgeRevealDistance);
            SetVisible(Direction.Down, inside && controller.CanTurnDown
                && localPoint.y <= rect.yMin + edgeRevealDistance);
        }

        private void SubscribeButtonClicks()
        {
            _buttonSubscriptions?.Dispose();

            if (controller == null)
            {
                _buttonSubscriptions = null;
                return;
            }

            // 활성화된 동안만 네 방향 클릭 스트림을 유지한다.
            var subscriptions = Disposable.CreateBuilder();

            if (leftButton != null)
            {
                leftButton.OnClickAsObservable()
                    .Subscribe(controller, static (_, target) => target.TurnLeft())
                    .AddTo(ref subscriptions);
            }

            if (rightButton != null)
            {
                rightButton.OnClickAsObservable()
                    .Subscribe(controller, static (_, target) => target.TurnRight())
                    .AddTo(ref subscriptions);
            }

            if (upButton != null)
            {
                upButton.OnClickAsObservable()
                    .Subscribe(controller, static (_, target) => target.TurnUp())
                    .AddTo(ref subscriptions);
            }

            if (downButton != null)
            {
                downButton.OnClickAsObservable()
                    .Subscribe(controller, static (_, target) => target.TurnDown())
                    .AddTo(ref subscriptions);
            }

            _buttonSubscriptions = subscriptions.Build();
        }

        private void SetVisible(Direction direction, bool visible)
        {
            // 상태가 바뀔 때만 트윈을 갱신한다.
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

            // 시간 정지 중에도 내비게이션 UI는 자연스럽게 표시한다.
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
