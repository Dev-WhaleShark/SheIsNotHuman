using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// 설치된 URP의 UberPost.shader/DistortUV와
    /// UberPostProcessPass.LensDistortionParams.CalcLensDistortionParams를 CPU에서 대응 계산한다.
    /// 화면 좌표는 RenderTexture를 포함한 카메라 출력의 좌하단 기준 픽셀이다.
    /// 렌즈 왜곡만 보정하며 Panini 투영, 사용자 정의 전체 화면 왜곡, XR은 지원하지 않는다.
    /// </summary>
    public static class LensDistortionCoordinates
    {
        private struct Parameters
        {
            public bool active;
            public Vector2 center, axis;
            public float theta, sigma, scale, intensity;
        }

        private static readonly Dictionary<Camera, Parameters> Rendered = new Dictionary<Camera, Parameters>();

        static LensDistortionCoordinates() => InstallCapture();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InstallCapture()
        {
            // 도메인 재로드를 생략한 재생에서도 이전 값과 중복 렌더 콜백이 남지 않게 한다.
            Rendered.Clear();
            RenderPipelineManager.endCameraRendering -= CaptureRenderedStack;
            RenderPipelineManager.endCameraRendering += CaptureRenderedStack;
        }

        private static void CaptureRenderedStack(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game) return;
            // 렌더 종료 시 이 카메라에 적용된 값을 복사한다. 스택 참조를 보관하면
            // 다음 카메라의 Volume 설정을 읽게 되어 화면과 입력 보정이 어긋날 수 있다.
            Rendered[camera] = IsPostProcessingEnabled(camera)
                ? Read(VolumeManager.instance.stack) : default;
        }

        private static bool IsPostProcessingEnabled(Camera camera) => camera != null
            && UniversalRenderPipeline.asset != null && !camera.stereoEnabled
            && camera.TryGetComponent<UniversalAdditionalCameraData>(out var data)
            && data.renderPostProcessing;

        private static Parameters GetParameters(Camera camera)
        {
            if (!IsPostProcessingEnabled(camera)) return default;
            if (Rendered.TryGetValue(camera, out var rendered)) return rendered;
            var manager = VolumeManager.instance;
            if (!manager.isInitialized) return default;
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            // ViaScripting 카메라는 URP와 동일하게 기존 고정 스택을 사용한다.
            if (Application.isPlaying && !data.requiresVolumeFrameworkUpdate && data.volumeStack != null)
                return Read(data.volumeStack);
            // 최초 렌더 전에는 별도 스택에서 같은 트리거/레이어를 평가한다.
            // 전역 VolumeManager.stack은 교체하거나 갱신하지 않아 실제 렌더 효과를 보존한다.
            var temporary = manager.CreateStack();
            try
            {
                manager.Update(temporary, data.volumeTrigger != null ? data.volumeTrigger : camera.transform,
                    data.volumeLayerMask);
                return Read(temporary);
            }
            // 평가 중 예외가 나도 임시 스택의 수명은 이 호출 안에서 끝낸다.
            finally { manager.DestroyStack(temporary); }
        }

        private static Parameters Read(VolumeStack stack)
        {
            var lens = stack?.GetComponent<LensDistortion>();
            if (lens == null || !lens.active || !lens.IsActive()) return default;
            return Create(lens.intensity.value, lens.xMultiplier.value, lens.yMultiplier.value,
                lens.center.value, lens.scale.value);
        }

        private static Parameters Create(float intensity, float x, float y, Vector2 center, float scale)
        {
            // 셰이더와 같은 파라미터 규약이다. 계수나 중심 변환을 바꾸면 화면과 입력이 달라진다.
            float amount = 1.6f * Mathf.Max(Mathf.Abs(intensity * 100f), 1f);
            float theta = Mathf.Deg2Rad * Mathf.Min(160f, amount);
            return new Parameters
            {
                active = intensity != 0f && (x > 0f || y > 0f),
                center = center * 2f - Vector2.one,
                axis = new Vector2(Mathf.Max(x, 1e-4f), Mathf.Max(y, 1e-4f)),
                theta = intensity >= 0f ? theta : 1f / theta,
                sigma = 2f * Mathf.Tan(theta * .5f), scale = 1f / scale, intensity = intensity
            };
        }

        private static Vector2 DistortUV(Vector2 uv, Parameters p)
        {
            if (!p.active) return uv;
            uv = (uv - Vector2.one * .5f) * p.scale + Vector2.one * .5f;
            Vector2 ruv = Vector2.Scale(p.axis, uv - Vector2.one * .5f - p.center);
            float radius = ruv.magnitude;
            // 음수 왜곡의 중심에서는 해석적 극한값을 사용해 0 * 무한대에 의한 미정값을 피한다.
            float factor = p.intensity > 0f
                ? Mathf.Tan(radius * p.theta) / (radius * p.sigma + 6.103515625e-5f)
                : (radius > 1e-8f ? p.theta * Mathf.Atan(radius * p.sigma) / radius : p.theta * p.sigma);
            return uv + ruv * (factor - 1f);
        }

        private static Vector2 Map(Camera camera, Vector2 screen, Parameters parameters)
        {
            if (camera == null || !parameters.active) return screen;
            Rect rect = camera.pixelRect;
            if (rect.width <= 0 || rect.height <= 0) return screen;
            Vector2 uv = new Vector2((screen.x - rect.x) / rect.width, (screen.y - rect.y) / rect.height);
            // Blit.hlsl/GetFullScreenTriangleTexCoord의 UNITY_UV_STARTS_AT_TOP 규약에 맞춰
            // 왜곡 계산 전후에만 Y를 뒤집고 외부에는 좌하단 픽셀 좌표를 유지한다.
            if (SystemInfo.graphicsUVStartsAtTop) uv.y = 1f - uv.y;
            uv = DistortUV(uv, parameters);
            if (SystemInfo.graphicsUVStartsAtTop) uv.y = 1f - uv.y;
            return rect.position + Vector2.Scale(uv, rect.size);
        }

        /// <summary>표시된 픽셀을 셰이더가 샘플링한 왜곡 전 투영 위치로 변환해 UI 판정에 사용한다.</summary>
        public static Vector2 ScreenToUndistorted(Camera camera, Vector2 screenPosition)
            => Map(camera, screenPosition, GetParameters(camera));

        /// <summary>왜곡 전 투영 위치가 표시될 픽셀을 수치적으로 근사한다. 왜곡이 없으면 그대로 반환한다.</summary>
        public static Vector2 UndistortedToScreen(Camera camera, Vector2 projectionPosition)
        {
            var p = GetParameters(camera);
            if (!p.active || camera == null) return projectionPosition;
            Vector2 screen = projectionPosition;
            // 축별 배율과 중심 이동까지 포함한 실제 샘플 매핑을 역산한다.
            // 반복 횟수와 특이 행렬 검사를 제한해 역변환이 불안정해도 무한 반복하지 않는다.
            for (int i = 0; i < 24; i++)
            {
                Vector2 mapped = Map(camera, screen, p);
                Vector2 error = mapped - projectionPosition;
                if (error.sqrMagnitude < .0001f) break;
                const float step = .25f;
                Vector2 dx = (Map(camera, screen + Vector2.right * step, p) - mapped) / step;
                Vector2 dy = (Map(camera, screen + Vector2.up * step, p) - mapped) / step;
                float determinant = dx.x * dy.y - dx.y * dy.x;
                if (Mathf.Abs(determinant) < 1e-8f) break;
                Vector2 correction = new Vector2(dy.y * error.x - dy.x * error.y,
                    dx.x * error.y - dx.y * error.x) / determinant;
                // 오차가 커지면 뉴턴 보정량을 줄여 탄젠트 특이점을 한 번에 넘어가는 것을 억제한다.
                float damping = 1f;
                while (damping > 1f / 128f &&
                    (Map(camera, screen - correction * damping, p) - projectionPosition).sqrMagnitude > error.sqrMagnitude)
                    damping *= .5f;
                screen -= correction * damping;
            }
            return screen;
        }

        /// <summary>물리 화면 좌표를 한 번 보정한 뒤 RectTransform의 로컬 평면 좌표로 변환한다.</summary>
        public static bool ScreenPointToLocalPoint(RectTransform rect, Vector2 screenPosition,
            Camera camera, out Vector2 localPoint) => RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, ScreenToUndistorted(camera, screenPosition), camera, out localPoint);

        /// <summary>씬/프로파일과 무관한 고정 셰이더 기준 벡터를 검사한다. 실제 GPU 렌더 검증은 별도로 필요하다.</summary>
        public static bool ValidateShaderReferenceVectors(out string report)
        {
            var positive = Create(.356f, 1f, .95f, new Vector2(.5f, .5f), 1.18f);
            var negative = Create(-.45f, .7f, 1f, new Vector2(.4f, .6f), 1.1f);
            float error = Mathf.Max(
                Vector2.Distance(DistortUV(new Vector2(.1f, .9f), positive), new Vector2(.16495917f, .83523794f)),
                Vector2.Distance(DistortUV(new Vector2(.2f, .8f), negative), new Vector2(.21963750f, .78363474f)));
            report = $"URP shader reference UV max error={error:G8}; tolerance=0.00001 (not rendered verification)";
            return error < .00001f;
        }
    }
}
