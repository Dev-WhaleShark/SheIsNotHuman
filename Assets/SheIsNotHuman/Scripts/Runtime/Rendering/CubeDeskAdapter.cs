using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using WhaleShark.Interaction;
using WhaleShark.UI;

namespace WhaleShark.Rendering
{
    /// <summary>Optional bridge between a local desk and the main scene's cube camera.</summary>
    [DisallowMultipleComponent]
    public sealed class CubeDeskAdapter : MonoBehaviour
    {
        [TabGroup("InspectorTabs", "연결"), FoldoutGroup("InspectorTabs/연결/외부 화면", Expanded=false), Required, SerializeField] private CubeCameraRig rig;
        [FoldoutGroup("InspectorTabs/연결/외부 화면"), Required, SerializeField] private ServiceDesk desk;
        [FoldoutGroup("InspectorTabs/연결/외부 화면"), Required, SerializeField] private DeskFocus focus;
        [FoldoutGroup("InspectorTabs/연결/외부 화면"), Required, SerializeField] private DeskInteractionContext context;
        [TabGroup("InspectorTabs", "설정"), Range(.2f,.9f), LabelText("확대 카메라 거리 비율"), SerializeField] private float focusDepthRatio = .7f;
        [ShowInInspector, ReadOnly, PropertyOrder(-10), LabelText("현재 면")]
        public CubeFace CurrentFace => rig == null ? CubeFace.Front : rig.CurrentFace;

        private void OnEnable()
        {
            if (context != null) context.CoordinateConverter = LensDistortionCoordinates.ScreenPointToLocalPoint;
            focus?.ConfigureProjection(rig == null ? null : rig.ViewCamera, focusDepthRatio);
            Refresh();
        }
        private void Update() => Refresh();
        private void Refresh()
        {
            if (rig == null || desk == null) return;
            desk.SetExternalInput(rig.isActiveAndEnabled && rig.CurrentFace == CubeFace.Bottom, rig.IsTurning);
            rig.InputBlocked = desk.BlocksCameraInput;
        }
        private void OnDisable()
        {
            if (context != null) context.CoordinateConverter = null;
            if (rig != null) rig.InputBlocked = false;
            desk?.SetExternalInput(true,false);
            focus?.ConfigureProjection(null,focusDepthRatio);
        }
        public IEnumerator FocusFront(bool animate) { yield return Focus(CubeFace.Front,animate); }
        public IEnumerator FocusBottom(bool animate) { yield return Focus(CubeFace.Bottom,animate); }
        private IEnumerator Focus(CubeFace face,bool animate)
        {
            if (rig == null || !isActiveAndEnabled) yield break;
            rig.FocusFace(face,animate);
            while (rig != null && rig.IsTurning && isActiveAndEnabled) yield return null;
            Refresh();
        }
        public void ResetConnection()
        { if (rig != null) { if(rig.IsTurning) rig.FocusFace(rig.CurrentFace,false); rig.InputBlocked=false; } Refresh(); }
        [TabGroup("InspectorTabs", "개발 도구"), Button("정면으로 이동"), DisableInEditorMode]
        private void DebugFront() { if (rig != null && desk != null && !desk.BlocksCameraInput) rig.FocusFace(CubeFace.Front,true); }
    }
}
