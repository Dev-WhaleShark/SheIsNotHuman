using Sirenix.OdinInspector;
using UnityEngine;

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// 각 면의 RenderTexture와 렌즈 설정을 화면 표면에 적용한다.
    /// 공유 머티리얼은 유지하고 면마다 다른 속성만 덮어쓴다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    [HideMonoScript]
    public sealed class CubeFaceLensDisplay : MonoBehaviour
    {
        // 문자열 검색을 피하도록 셰이더 속성 ID를 캐시한다.
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int LensCenterId = Shader.PropertyToID("_LensCenter");
        private static readonly int DistortionId = Shader.PropertyToID("_Distortion");
        private static readonly int Distortion2Id = Shader.PropertyToID("_Distortion2");
        private static readonly int ZoomId = Shader.PropertyToID("_Zoom");
        private static readonly int ChromaticAberrationId = Shader.PropertyToID("_ChromaticAberration");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");

        [TitleGroup("화면 소스")]
        [Required("표시할 텍스처가 필요합니다."), AssetsOnly]
        [LabelText("Render Texture")]
        [SerializeField] private Texture sourceTexture;

        [TitleGroup("화면 소스")]
        [InfoBox("현재 프로젝트의 면 화면 기준은 16:9입니다.", InfoMessageType.Warning, nameof(HasNonStandardAspect))]
        [LabelText("화면 비율")]
        [SerializeField, Min(0.01f)] private float aspect = 1f;

        [TitleGroup("렌즈 프로파일")]
        [LabelText("렌즈 중심")]
        [SerializeField] private Vector2 lensCenter = new(0.5f, 0.5f);

        [TitleGroup("렌즈 프로파일")]
        [LabelText("왜곡")]
        [SerializeField, Range(-1f, 1f)] private float distortion;

        [TitleGroup("렌즈 프로파일")]
        [LabelText("가장자리 왜곡")]
        [SerializeField, Range(-1f, 1f)] private float edgeDistortion;

        [TitleGroup("렌즈 프로파일")]
        [LabelText("확대")]
        [SerializeField, Range(0.5f, 2f)] private float zoom = 1f;

        [TitleGroup("렌즈 프로파일")]
        [LabelText("색수차")]
        [SerializeField, Range(0f, 0.05f)] private float chromaticAberration;

        [TitleGroup("렌즈 프로파일")]
        [LabelText("비네팅")]
        [SerializeField, Range(0f, 1f)] private float vignette;

        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propertyBlock;

        private void OnEnable()
        {
            CacheComponents();
            ApplyProperties();
        }

        private void OnValidate()
        {
            // 에디터에서 값을 바꾸면 렌즈 미리보기를 즉시 갱신한다.
            aspect = Mathf.Max(0.01f, aspect);
            zoom = Mathf.Clamp(zoom, 0.5f, 2f);
            CacheComponents();
            ApplyProperties();
        }

        /// <summary>Inspector 또는 외부 호출로 현재 렌즈 설정을 해당 면의 Renderer에 다시 적용한다.</summary>
        [ContextMenu("Refresh Lens")]
        [Button("렌즈 즉시 갱신", ButtonSizes.Medium)]
        public void RefreshLens()
        {
            CacheComponents();
            ApplyProperties();
        }

        private void CacheComponents()
        {
            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
            }

            // 면별 값을 적용해도 공유 머티리얼 원본은 변경하지 않는다.
            _propertyBlock ??= new MaterialPropertyBlock();
        }

        private void ApplyProperties()
        {
            if (_meshRenderer == null || _propertyBlock == null)
            {
                return;
            }

            // 다른 시스템이 넣은 속성을 보존한 뒤 렌즈 값만 갱신한다.
            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(BaseMapId, sourceTexture != null ? sourceTexture : Texture2D.blackTexture);
            _propertyBlock.SetVector(LensCenterId, lensCenter);
            _propertyBlock.SetFloat(DistortionId, distortion);
            _propertyBlock.SetFloat(Distortion2Id, edgeDistortion);
            _propertyBlock.SetFloat(ZoomId, zoom);
            _propertyBlock.SetFloat(ChromaticAberrationId, chromaticAberration);
            _propertyBlock.SetFloat(VignetteId, vignette);
            _propertyBlock.SetFloat(AspectId, aspect);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        private bool HasNonStandardAspect()
        {
            return !Mathf.Approximately(aspect, 16f / 9f);
        }
    }
}
