using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace SheIsNotHuman.CubeScreen
{
    /// <summary>
    /// 화면 렌더링에는 면별 캡처 카메라를 사용하고,
    /// UI 입력 판정에는 뷰어 기준 원근 카메라를 사용한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [HideMonoScript]
    public sealed class CubeFaceGraphicRaycaster : GraphicRaycaster
    {
        [Required, SceneObjectsOnly]
        [LabelText("입력 판정 카메라")]
        [SerializeField] private Camera inputCamera;

        private CubeScreenController _controller;
        private CubeFace _face;
        private bool _hasValidFace;

        [ShowInInspector, ReadOnly, HideInEditorMode]
        [LabelText("판정 면")]
        private CubeFace ResolvedFace => _face;

        [ShowInInspector, ReadOnly, HideInEditorMode]
        [LabelText("입력 허용")]
        private bool CanReceiveInput => _controller != null
            && _hasValidFace
            && _controller.CanReceiveInput(_face);

        /// <summary>캡처 카메라와 별개로, 사용자가 보는 투영에 맞는 UI 판정 카메라를 제공한다.</summary>
        public override Camera eventCamera => inputCamera != null ? inputCamera : base.eventCamera;

        protected override void Awake()
        {
            base.Awake();

            // 씬 연결을 건드리지 않고 현재 육면체 컨트롤러를 한 번만 찾는다.
            _controller = FindAnyObjectByType<CubeScreenController>();
            _hasValidFace = TryResolveFace(gameObject.name, out _face);

            if (_controller == null)
            {
                Debug.LogError("CubeScreenController를 찾을 수 없어 면 UI 입력을 차단합니다.", this);
            }

            if (!_hasValidFace)
            {
                Debug.LogError($"'{gameObject.name}'에서 육면체 면을 판별할 수 없어 UI 입력을 차단합니다.", this);
            }
        }

        /// <summary>전환 중이거나 현재 면이 아니면 EventSystem의 입력 후보에서 제외한다.</summary>
        public override bool IsActive()
        {
            // EventSystem 단계에서 현재 면 외의 모든 Raycast를 제외한다.
            return base.IsActive() && CanReceiveInput;
        }

        private static bool TryResolveFace(string objectName, out CubeFace face)
        {
            // 기존 씬의 이름 규약인 마지막 '_면이름'을 사용하므로 오브젝트 이름도 연결 계약이다.
            int separatorIndex = objectName.LastIndexOf('_');
            if (separatorIndex < 0 || separatorIndex >= objectName.Length - 1)
            {
                face = default;
                return false;
            }

            string faceName = objectName.Substring(separatorIndex + 1);
            return Enum.TryParse(faceName, true, out face);
        }
    }
}
