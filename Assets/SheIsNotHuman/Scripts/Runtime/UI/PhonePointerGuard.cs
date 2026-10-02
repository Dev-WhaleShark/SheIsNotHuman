using UnityEngine;
using UnityEngine.EventSystems;

namespace WhaleShark.UI
{
    /// <summary>Consumes pointer down at the phone leaf so desk wrappers cannot capture app scrolling.</summary>
    [DisallowMultipleComponent]
    public sealed class PhonePointerGuard : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public void OnPointerDown(PointerEventData eventData) { }
        public void OnPointerUp(PointerEventData eventData) { }
        public void OnPointerClick(PointerEventData eventData) { }
    }
}
