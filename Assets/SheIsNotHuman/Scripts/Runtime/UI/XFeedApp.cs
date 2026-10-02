using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace WhaleShark.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(PhonePointerGuard))]
    public sealed class XFeedApp : MonoBehaviour
    {
        [TabGroup("InspectorTabs", "연결"), FoldoutGroup("InspectorTabs/연결/참조", Expanded = false), Required, SerializeField]
        private ScrollRect feed;
        [TabGroup("InspectorTabs", "설정"), LabelText("처음 위치"), SerializeField]
        private Vector2 initialScroll = new(0, 1);
        [ShowInInspector, ReadOnly, PropertyOrder(-20), LabelText("스크롤 위치")]
        public Vector2 ScrollPosition => feed == null ? initialScroll : feed.normalizedPosition;
        [TabGroup("InspectorTabs", "개발 도구"), Button("피드 초기화")]
        public void ResetFeed()
        {
            if (feed == null) return;
            feed.StopMovement(); feed.normalizedPosition = initialScroll;
        }
        public void SetInputEnabled(bool allowed) { if (feed != null) { feed.enabled = allowed; if (!allowed) feed.StopMovement(); } }
        private void OnDisable() { if (feed != null) feed.StopMovement(); }
    }
}
