using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// CPU equivalent of installed URP UberPost.shader/DistortUV and
    /// UberPostProcessPass.LensDistortionParams.CalcLensDistortionParams.
    /// Screen coordinates are bottom-left pixels in the camera output (including RenderTextures).
    /// Panini projection, custom fullscreen warps and XR are not mapped by this Lens-only adapter.
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
            Rendered.Clear();
            RenderPipelineManager.endCameraRendering -= CaptureRenderedStack;
            RenderPipelineManager.endCameraRendering += CaptureRenderedStack;
        }

        private static void CaptureRenderedStack(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game) return;
            // At endCameraRendering URP's main stack still belongs to this camera. Copy values:
            // keeping the stack reference would read the next camera's volumes instead.
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
            // ViaScripting cameras use their existing frozen stack, just as URP does.
            if (Application.isPlaying && !data.requiresVolumeFrameworkUpdate && data.volumeStack != null)
                return Read(data.volumeStack);
            // Before the first render evaluate the same trigger/layer mask in a private stack.
            // Never replace/update VolumeManager.stack or change the rendered effect.
            var temporary = manager.CreateStack();
            try
            {
                manager.Update(temporary, data.volumeTrigger != null ? data.volumeTrigger : camera.transform,
                    data.volumeLayerMask);
                return Read(temporary);
            }
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
            // The negative branch's analytic limit avoids shader's undefined 0 * infinity at center.
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
            // Blit.hlsl/GetFullScreenTriangleTexCoord uses UNITY_UV_STARTS_AT_TOP.
            if (SystemInfo.graphicsUVStartsAtTop) uv.y = 1f - uv.y;
            uv = DistortUV(uv, parameters);
            if (SystemInfo.graphicsUVStartsAtTop) uv.y = 1f - uv.y;
            return rect.position + Vector2.Scale(uv, rect.size);
        }

        public static Vector2 ScreenToUndistorted(Camera camera, Vector2 screenPosition)
            => Map(camera, screenPosition, GetParameters(camera));

        public static Vector2 UndistortedToScreen(Camera camera, Vector2 projectionPosition)
        {
            var p = GetParameters(camera);
            if (!p.active || camera == null) return projectionPosition;
            Vector2 screen = projectionPosition;
            // Invert the actual sample mapping, including unequal axes and off-center settings.
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
                // Backtracking prevents a Newton step jumping across a tangent pole.
                float damping = 1f;
                while (damping > 1f / 128f &&
                    (Map(camera, screen - correction * damping, p) - projectionPosition).sqrMagnitude > error.sqrMagnitude)
                    damping *= .5f;
                screen -= correction * damping;
            }
            return screen;
        }

        public static bool ScreenPointToLocalPoint(RectTransform rect, Vector2 screenPosition,
            Camera camera, out Vector2 localPoint) => RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, ScreenToUndistorted(camera, screenPosition), camera, out localPoint);

        /// <summary>Fixed shader reference vectors, independent of a scene/profile; GPU tests are still required.</summary>
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
