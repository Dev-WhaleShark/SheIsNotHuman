using UnityEngine;

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// Lets a face use an orthographic camera for RenderTexture capture while
    /// pointer raycasts continue to use the viewer-aligned perspective camera.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class CubeFaceGraphicRaycaster : UnityEngine.UI.GraphicRaycaster
    {
        [SerializeField] private Camera inputCamera;

        public override Camera eventCamera => inputCamera != null ? inputCamera : base.eventCamera;
    }
}
