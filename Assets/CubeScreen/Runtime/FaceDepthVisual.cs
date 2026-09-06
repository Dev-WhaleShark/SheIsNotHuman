using Sirenix.OdinInspector;
using UnityEngine;

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// 객체 깊이를 가짜 원근과 픽셀 블러 값으로 변환한다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [HideMonoScript]
    public sealed class FaceDepthVisual : MonoBehaviour
    {
        private static readonly int PixelBlurRadiusId = Shader.PropertyToID("_PixelBlurRadius");
        private static readonly int LocalWarpId = Shader.PropertyToID("_LocalWarp");

        [TitleGroup("참조")]
        [Required, AssetsOnly]
        [LabelText("깊이 프로파일")]
        [SerializeField] private FaceDepthEffectProfile profile;

        [TitleGroup("참조")]
        [Required, SceneObjectsOnly]
        [LabelText("캡처 카메라")]
        [SerializeField] private Camera captureCamera;

        [TitleGroup("참조")]
        [Required, SceneObjectsOnly]
        [LabelText("시각 루트")]
        [SerializeField] private Transform visualRoot;

        [TitleGroup("참조")]
        [LabelText("효과 Renderer")]
        [SerializeField] private Renderer[] targetRenderers;

        [TitleGroup("객체별 보정")]
        [LabelText("블러 제외")]
        [SerializeField] private bool excludeBlur;

        [TitleGroup("객체별 보정")]
        [LabelText("블러 직접 지정")]
        [SerializeField] private bool overrideBlur;

        [TitleGroup("객체별 보정")]
        [ShowIf(nameof(overrideBlur))]
        [LabelText("블러 반경")]
        [SerializeField, Range(0, 3)] private int blurRadius;

        [TitleGroup("객체별 보정")]
        [LabelText("크기 직접 지정")]
        [SerializeField] private bool overrideScale;

        [TitleGroup("객체별 보정")]
        [ShowIf(nameof(overrideScale))]
        [LabelText("크기 배율")]
        [SerializeField, Min(0.01f)] private float scaleMultiplier = 1f;

        [TitleGroup("객체별 보정")]
        [LabelText("로컬 렌즈")]
        [SerializeField, Range(-0.5f, 0.5f)] private float localWarp;

        [TitleGroup("기준 Transform")]
        [LabelText("기준 위치")]
        [SerializeField] private Vector3 baseLocalPosition;

        [TitleGroup("기준 Transform")]
        [LabelText("기준 크기")]
        [SerializeField] private Vector3 baseVisualScale = Vector3.one;

        [TitleGroup("기준 Transform")]
        [SerializeField, HideInInspector] private bool hasBasePose;

        private MaterialPropertyBlock _propertyBlock;

        private void OnEnable()
        {
            EnsureBasePose();
            ApplyEffects();
        }

        private void OnValidate()
        {
            blurRadius = Mathf.Clamp(blurRadius, 0, 3);
            scaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
            EnsureBasePose();
            ApplyEffects();
        }

        [Button("현재 Transform을 기준값으로 저장", ButtonSizes.Medium)]
        public void CaptureBasePose()
        {
            baseLocalPosition = transform.localPosition;
            baseVisualScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            hasBasePose = true;
            ApplyEffects();
        }

        [Button("깊이 효과 갱신", ButtonSizes.Medium)]
        public void ApplyEffects()
        {
            if (profile == null || captureCamera == null || visualRoot == null)
            {
                return;
            }

            float viewDepth = Vector3.Dot(
                transform.position - captureCamera.transform.position,
                captureCamera.transform.forward);

            float spread = profile.EvaluateSpread(viewDepth);
            float scale = overrideScale ? scaleMultiplier : profile.EvaluateScale(viewDepth);

            transform.localPosition = new Vector3(
                baseLocalPosition.x * spread,
                baseLocalPosition.y * spread,
                baseLocalPosition.z);
            visualRoot.localScale = baseVisualScale * scale;

            int resolvedBlur = excludeBlur
                ? 0
                : overrideBlur
                    ? blurRadius
                    : profile.EvaluateBlurRadius(viewDepth);

            ApplyRendererProperties(resolvedBlur);
        }

        public void Initialize(
            FaceDepthEffectProfile depthProfile,
            Camera faceCamera,
            Transform targetVisualRoot,
            Renderer[] renderers,
            bool shouldExcludeBlur,
            bool shouldOverrideBlur,
            int manualBlurRadius,
            float warp)
        {
            profile = depthProfile;
            captureCamera = faceCamera;
            visualRoot = targetVisualRoot;
            targetRenderers = renderers;
            excludeBlur = shouldExcludeBlur;
            overrideBlur = shouldOverrideBlur;
            blurRadius = Mathf.Clamp(manualBlurRadius, 0, 3);
            localWarp = Mathf.Clamp(warp, -0.5f, 0.5f);
            CaptureBasePose();
        }

        private void EnsureBasePose()
        {
            if (hasBasePose)
            {
                return;
            }

            baseLocalPosition = transform.localPosition;
            baseVisualScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            hasBasePose = true;
        }

        private void ApplyRendererProperties(int resolvedBlur)
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();

            foreach (Renderer targetRenderer in targetRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(PixelBlurRadiusId, resolvedBlur);
                _propertyBlock.SetFloat(LocalWarpId, localWarp);
                targetRenderer.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
