using Sirenix.OdinInspector;
using SheIsNotHuman.CubeScreen;
using UnityEngine;
using UnityEngine.UI;

namespace SheIsNotHuman.InspectionMvp
{
    /// <summary>OrderDocument에 내장된 휴대폰의 페이지와 입력 상태를 관리한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class PhoneDeviceView : MonoBehaviour
    {
        [Required] public GameObject homePage;
        [Required] public GameObject witchformPage;
        [Required] public GameObject xPage;
        [Required] public Button witchformButton;
        [Required] public Button xButton;
        [Required] public Button homeButton;
        [Required] public Button backButton;

        private enum Page { Home, Witchform, X }

        private CanvasGroup inputGroup;
        private CanvasGroup witchformInputGroup;
        private CanvasGroup xInputGroup;
        private CanvasGroup homeInputGroup;
        private CanvasGroup backInputGroup;
        private OrderDocumentView orderDocument;
        private InspectionMvpView inspectionView;
        private Page currentPage;
        private bool focused;

        private void Awake()
        {
            inputGroup = GetComponent<CanvasGroup>();
            witchformInputGroup = EnsureInputGroup(witchformButton);
            xInputGroup = EnsureInputGroup(xButton);
            homeInputGroup = EnsureInputGroup(homeButton);
            backInputGroup = EnsureInputGroup(backButton);
            orderDocument = GetComponentInParent<OrderDocumentView>();
            inspectionView = GetComponentInParent<InspectionMvpView>();
            focused = false;
            ResetToHome();
        }

        private void OnEnable()
        {
            if (witchformButton != null) witchformButton.onClick.AddListener(OpenWitchform);
            if (xButton != null) xButton.onClick.AddListener(OpenX);
            if (homeButton != null) homeButton.onClick.AddListener(GoHome);
            if (backButton != null) backButton.onClick.AddListener(GoBack);
            Refresh();
        }

        private void Update() => RefreshInput();

        private void OnDisable()
        {
            if (witchformButton != null) witchformButton.onClick.RemoveListener(OpenWitchform);
            if (xButton != null) xButton.onClick.RemoveListener(OpenX);
            if (homeButton != null) homeButton.onClick.RemoveListener(GoHome);
            if (backButton != null) backButton.onClick.RemoveListener(GoBack);
            SetFocused(false);
            ResetToHome();
        }

        public void SetFocused(bool value)
        {
            focused = value;
            Refresh();
        }

        public void ResetToHome()
        {
            currentPage = Page.Home;
            Refresh();
        }

        private void OpenWitchform()
        {
            if (!CanNavigate()) return;
            currentPage = Page.Witchform;
            Refresh();
        }

        private void OpenX()
        {
            if (!CanNavigate()) return;
            currentPage = Page.X;
            Refresh();
        }

        private void GoHome()
        {
            if (!CanNavigate()) return;
            ResetToHome();
        }

        private void GoBack()
        {
            if (!CanNavigate() || currentPage == Page.Home) return;
            ResetToHome();
        }

        private bool CanNavigate()
        {
            if (!focused || !isActiveAndEnabled) return false;
            if (orderDocument == null) orderDocument = GetComponentInParent<OrderDocumentView>();
            if (inspectionView == null) inspectionView = GetComponentInParent<InspectionMvpView>();
            if (orderDocument == null || inspectionView == null || inspectionView.navigation == null) return false;
            var navigation = inspectionView.navigation;
            return inspectionView.IsAnyModalOpen && !inspectionView.IsModalBusy
                && navigation.isActiveAndEnabled && !navigation.IsTurning
                && navigation.CurrentFace == CubeFace.Bottom
                && inspectionView.FocusedOrder == orderDocument.gameObject;
        }

        private static CanvasGroup EnsureInputGroup(Button button)
        {
            if (button == null) return null;
            var group = button.GetComponent<CanvasGroup>();
            return group != null ? group : button.gameObject.AddComponent<CanvasGroup>();
        }

        private void Refresh()
        {
            if (homePage != null) homePage.SetActive(currentPage == Page.Home);
            if (witchformPage != null) witchformPage.SetActive(currentPage == Page.Witchform);
            if (xPage != null) xPage.SetActive(currentPage == Page.X);
            RefreshInput();
        }

        private void RefreshInput()
        {
            bool canUseButtons = CanNavigate();
            if (inputGroup == null) inputGroup = GetComponent<CanvasGroup>();
            if (inputGroup != null)
            {
                // This component shares the compact document's root Button. Keep its raycast path open.
                inputGroup.interactable = true;
                inputGroup.blocksRaycasts = true;
            }
            SetButtonInput(witchformInputGroup, canUseButtons);
            SetButtonInput(xInputGroup, canUseButtons);
            SetButtonInput(homeInputGroup, canUseButtons);
            SetButtonInput(backInputGroup, canUseButtons && currentPage != Page.Home);
            if (witchformButton != null) witchformButton.interactable = canUseButtons;
            if (xButton != null) xButton.interactable = canUseButtons;
            if (homeButton != null) homeButton.interactable = canUseButtons;
            if (backButton != null) backButton.interactable = canUseButtons && currentPage != Page.Home;
        }

        private static void SetButtonInput(CanvasGroup group, bool enabled)
        {
            if (group == null) return;
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }
    }
}
