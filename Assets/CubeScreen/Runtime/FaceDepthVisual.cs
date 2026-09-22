using Sirenix.OdinInspector;
using UnityEngine;

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// 객체 깊이를 가짜 원근과 픽셀 블러 값으로 변환한다.
    /// 저장된 기준 자세에서 효과를 계산하며 에디터 미리보기와 런타임 갱신에 같은 경로를 사용한다.
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

        /// <summary>현재 자세를 새 기준으로 채택한 뒤 효과를 적용한다. 재호출하면 기준도 바뀐다.</summary>
        [Button("현재 Transform을 기준값으로 저장", ButtonSizes.Medium)]
        public void CaptureBasePose()
        {
            baseLocalPosition = transform.localPosition;
            baseVisualScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
            hasBasePose = true;
            ApplyEffects();
        }

        /// <summary>카메라 전방 깊이로 크기·위치·블러를 갱신한다. 자동 매 프레임 갱신은 하지 않는다.</summary>
        [Button("깊이 효과 갱신", ButtonSizes.Medium)]
        public void ApplyEffects()
        {
            if (profile == null || captureCamera == null || visualRoot == null)
            {
                return;
            }

            // 유클리드 거리가 아니라 카메라 전방 성분을 사용해 같은 깊이 평면의 효과를 맞춘다.
            float viewDepth = Vector3.Dot(
                transform.position - captureCamera.transform.position,
                captureCamera.transform.forward);

            float spread = profile.EvaluateSpread(viewDepth);
            float scale = overrideScale ? scaleMultiplier : profile.EvaluateScale(viewDepth);

            // 현재 값에 배율을 누적하지 않는다. 저장된 XY와 크기에만 배율을 적용하고 Z는 보존한다.
            transform.localPosition = new Vector3(
                baseLocalPosition.x * spread,
                baseLocalPosition.y * spread,
                baseLocalPosition.z);
            visualRoot.localScale = baseVisualScale * scale;

            // 블러 제외가 수동 지정과 프로파일보다 우선한다.
            int resolvedBlur = excludeBlur
                ? 0
                : overrideBlur
                    ? blurRadius
                    : profile.EvaluateBlurRadius(viewDepth);

            ApplyRendererProperties(resolvedBlur);
        }

        /// <summary>생성 도구가 참조와 개별 보정을 연결하고 현재 자세를 기준으로 최초 효과를 적용한다.</summary>
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

                // 공유 머티리얼과 다른 효과의 속성을 보존하고 이 컴포넌트 담당 값만 덮어쓴다.
                targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(PixelBlurRadiusId, resolvedBlur);
                _propertyBlock.SetFloat(LocalWarpId, localWarp);
                targetRenderer.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
