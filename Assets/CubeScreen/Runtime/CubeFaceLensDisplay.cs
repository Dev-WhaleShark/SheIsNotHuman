using UnityEngine;

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// Applies one face's RenderTexture and lens profile to its display surface.
    /// MaterialPropertyBlock keeps the shared lens material reusable across all faces.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class CubeFaceLensDisplay : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int LensCenterId = Shader.PropertyToID("_LensCenter");
        private static readonly int DistortionId = Shader.PropertyToID("_Distortion");
        private static readonly int Distortion2Id = Shader.PropertyToID("_Distortion2");
        private static readonly int ZoomId = Shader.PropertyToID("_Zoom");
        private static readonly int ChromaticAberrationId = Shader.PropertyToID("_ChromaticAberration");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");

        [Header("Source")]
        [SerializeField] private Texture sourceTexture;
        [SerializeField, Min(0.01f)] private float aspect = 1f;

        [Header("Lens Profile")]
        [SerializeField] private Vector2 lensCenter = new(0.5f, 0.5f);
        [SerializeField, Range(-1f, 1f)] private float distortion;
        [SerializeField, Range(-1f, 1f)] private float edgeDistortion;
        [SerializeField, Range(0.5f, 2f)] private float zoom = 1f;
        [SerializeField, Range(0f, 0.05f)] private float chromaticAberration;
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
            aspect = Mathf.Max(0.01f, aspect);
            zoom = Mathf.Clamp(zoom, 0.5f, 2f);
            CacheComponents();
            ApplyProperties();
        }

        [ContextMenu("Refresh Lens")]
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

            _propertyBlock ??= new MaterialPropertyBlock();
        }

        private void ApplyProperties()
        {
            if (_meshRenderer == null || _propertyBlock == null)
            {
                return;
            }

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
    }
}
