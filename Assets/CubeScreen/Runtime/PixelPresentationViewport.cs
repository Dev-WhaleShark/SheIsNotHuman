using UnityEngine;

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// Keeps pointer projection aligned with the fixed 16:9 pixel frame when the
    /// game window uses another aspect ratio and the presentation is letterboxed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PixelPresentationViewport : MonoBehaviour
    {
        [SerializeField] private Camera eventCamera;
        [SerializeField] private Vector2Int referenceResolution = new(640, 360);

        private int _screenWidth;
        private int _screenHeight;

        private void OnEnable()
        {
            RefreshViewport();
        }

        private void Update()
        {
            if (_screenWidth != Screen.width || _screenHeight != Screen.height)
            {
                RefreshViewport();
            }
        }

        private void OnDisable()
        {
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
        public void RefreshViewport()
        {
            _screenWidth = Mathf.Max(1, Screen.width);
            _screenHeight = Mathf.Max(1, Screen.height);

            if (eventCamera == null)
            {
                return;
            }

            float targetAspect = (float)referenceResolution.x / referenceResolution.y;
            float screenAspect = (float)_screenWidth / _screenHeight;

            if (screenAspect > targetAspect)
            {
                float width = targetAspect / screenAspect;
                eventCamera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }
            else
            {
                float height = screenAspect / targetAspect;
                eventCamera.rect = new Rect(0f, (1f - height) * 0.5f, 1f, height);
            }
        }
    }
}
