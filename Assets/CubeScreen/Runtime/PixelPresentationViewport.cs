using System;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// 고정 픽셀 화면비에 맞춰 이벤트 카메라의 뷰포트를 조정한다.
    /// 레터박스가 생겨도 화면과 포인터 좌표가 어긋나지 않게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    [HideMonoScript]
    public sealed class PixelPresentationViewport : MonoBehaviour
    {
        [BoxGroup("픽셀 프레젠테이션")]
        [Required, SceneObjectsOnly, LabelText("이벤트 카메라")]
        [SerializeField] private Camera eventCamera;

        [BoxGroup("픽셀 프레젠테이션")]
        [InfoBox("기준 해상도는 16:9 사용을 권장합니다.", InfoMessageType.Warning, nameof(HasNonStandardAspect))]
        [LabelText("기준 해상도")]
        [SerializeField] private Vector2Int referenceResolution = new(640, 360);

        private IDisposable _resolutionSubscription;

        private void OnEnable()
        {
            // 최초 해상도와 이후 변경 값만 전달받는다.
            _resolutionSubscription?.Dispose();
            _resolutionSubscription = Observable.EveryValueChanged(
                    this,
                    static _ => new Vector2Int(Screen.width, Screen.height),
                    UnityFrameProvider.Update)
                .Subscribe(ApplyViewport);
        }

        private void OnDisable()
        {
            _resolutionSubscription?.Dispose();
            _resolutionSubscription = null;

            // 컴포넌트를 끄면 카메라를 전체 화면으로 되돌린다.
            if (eventCamera != null)
            {
                eventCamera.rect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        private void OnValidate()
        {
            referenceResolution.x = Mathf.Max(1, referenceResolution.x);
            referenceResolution.y = Mathf.Max(1, referenceResolution.y);
        }

        [ContextMenu("Refresh Pixel Viewport")]
        [Button("뷰포트 즉시 갱신", ButtonSizes.Medium)]
        public void RefreshViewport()
        {
            ApplyViewport(new Vector2Int(Screen.width, Screen.height));
        }

        private void ApplyViewport(Vector2Int screenResolution)
        {
            // 0으로 나누는 상황을 막기 위해 최소 크기를 보장한다.
            int screenWidth = Mathf.Max(1, screenResolution.x);
            int screenHeight = Mathf.Max(1, screenResolution.y);

            if (eventCamera == null)
            {
                return;
            }

            float targetAspect = (float)referenceResolution.x / referenceResolution.y;
            float screenAspect = (float)screenWidth / screenHeight;

            if (screenAspect > targetAspect)
            {
                // 화면이 더 넓으면 좌우에 여백을 둔다.
                float width = targetAspect / screenAspect;
                eventCamera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }
            else
            {
                // 화면이 더 좁으면 상하에 여백을 둔다.
                float height = screenAspect / targetAspect;
                eventCamera.rect = new Rect(0f, (1f - height) * 0.5f, 1f, height);
            }
        }

        private bool HasNonStandardAspect()
        {
            return referenceResolution.y > 0
                && !Mathf.Approximately((float)referenceResolution.x / referenceResolution.y, 16f / 9f);
        }
    }
}
