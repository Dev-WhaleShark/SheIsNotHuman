using Sirenix.OdinInspector;
using UnityEngine;

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// Orthographic 면에서 깊이를 화면 연출값으로 변환한다.
    /// </summary>
    [CreateAssetMenu(fileName = "FaceDepthEffectProfile", menuName = "She Is Not Human/Cube Screen/Depth Effect Profile")]
    [HideMonoScript]
    public sealed class FaceDepthEffectProfile : ScriptableObject
    {
        [TitleGroup("깊이 범위")]
        [LabelText("가까운 깊이")]
        [SerializeField, Min(0f)] private float nearDepth = 2.2f;

        [TitleGroup("깊이 범위")]
        [LabelText("먼 깊이")]
        [SerializeField, Min(0.01f)] private float farDepth = 7.9f;

        [TitleGroup("가짜 원근")]
        [LabelText("깊이별 크기")]
        [SerializeField] private AnimationCurve scaleByDepth = new(
            new Keyframe(0f, 0.92f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 1.1f));

        [TitleGroup("가짜 원근")]
        [LabelText("중심 확장")]
        [SerializeField] private AnimationCurve spreadByDepth = new(
            new Keyframe(0f, 0.98f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 1.05f));

        [TitleGroup("초점 블러")]
        [LabelText("초점 깊이")]
        [SerializeField, Min(0f)] private float focusDepth = 5.2f;

        [TitleGroup("초점 블러")]
        [LabelText("최대 블러 거리")]
        [SerializeField, Min(0.01f)] private float maxBlurDistance = 2.2f;

        [TitleGroup("초점 블러")]
        [LabelText("최대 블러 반경")]
        [SerializeField, Range(0, 3)] private int maxBlurRadius = 3;

        /// <summary>카메라 전방 깊이를 먼 쪽 0, 가까운 쪽 1인 곡선 입력으로 정규화한다.</summary>
        public float EvaluateDepth01(float viewDepth)
        {
            float range = Mathf.Max(0.01f, farDepth - nearDepth);
            return Mathf.Clamp01((farDepth - viewDepth) / range);
        }

        /// <summary>정규화 깊이의 크기 곡선을 평가하며 시각 루트가 사라지지 않도록 양수를 보장한다.</summary>
        public float EvaluateScale(float viewDepth)
        {
            return Mathf.Max(0.01f, scaleByDepth.Evaluate(EvaluateDepth01(viewDepth)));
        }

        /// <summary>기준 위치의 XY 확장 배율을 반환하며 음수 반전은 허용하지 않는다.</summary>
        public float EvaluateSpread(float viewDepth)
        {
            return Mathf.Max(0f, spreadByDepth.Evaluate(EvaluateDepth01(viewDepth)));
        }

        /// <summary>초점 앞뒤의 거리를 동일하게 취급해 셰이더용 정수 블러 반경을 반환한다.</summary>
        public int EvaluateBlurRadius(float viewDepth)
        {
            float distance = Mathf.Abs(viewDepth - focusDepth);
            float normalized = Mathf.Clamp01(distance / Mathf.Max(0.01f, maxBlurDistance));
            return Mathf.Clamp(Mathf.RoundToInt(normalized * maxBlurRadius), 0, maxBlurRadius);
        }

        private void OnValidate()
        {
            nearDepth = Mathf.Max(0f, nearDepth);
            farDepth = Mathf.Max(nearDepth + 0.01f, farDepth);
            maxBlurDistance = Mathf.Max(0.01f, maxBlurDistance);
            maxBlurRadius = Mathf.Clamp(maxBlurRadius, 0, 3);
        }
    }
}
