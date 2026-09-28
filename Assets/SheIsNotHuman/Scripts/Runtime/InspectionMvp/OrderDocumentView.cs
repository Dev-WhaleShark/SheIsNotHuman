using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SheIsNotHuman.InspectionMvp
{
    /// <summary>주문서 원본의 간략/확대 표시를 담당한다. 판정은 표시 문자열이 아닌 원본 고객 코드를 사용한다.</summary>
    [DisallowMultipleComponent]
    public sealed class OrderDocumentView : MonoBehaviour
    {
        [Required] public TMP_Text content;
        public TMP_Text customerNameText;
        public TMP_Text customerCodeText;
        public TMP_Text orderNumberText;
        public TMP_Text productText;
        public TMP_Text quantityText;
        public bool expanded;
        private InspectionNpcData boundNpc;
        private Transform phoneRoot, homeScreen, witchformScreen, xScreen;
        private CanvasGroup phoneInput;
        private UnityEngine.UI.Button witchformAppButton, xAppButton, homeButton, backButton;
        private PhoneScreen currentScreen;
        private bool inputEnabled;

        private enum PhoneScreen { Home, Witchform, X }
        private bool HasAuthoredLayout => customerNameText != null || customerCodeText != null ||
            orderNumberText != null || productText != null || quantityText != null;

        private void Awake() { ResolvePhone(); ShowHome(); SetInputEnabled(false); }
        private void OnDestroy() { UnwirePhone(); }

        private void ResolvePhone()
        {
            if (phoneRoot == null) phoneRoot = transform.Find("PhoneRoot");
            if (phoneRoot == null) return;
            if (phoneInput == null) phoneInput = phoneRoot.GetComponent<CanvasGroup>();
            if (phoneInput == null) phoneInput = phoneRoot.gameObject.AddComponent<CanvasGroup>();
            if (homeScreen == null) homeScreen = phoneRoot.Find("HomeScreen");
            if (witchformScreen == null) witchformScreen = phoneRoot.Find("WitchformScreen");
            if (xScreen == null) xScreen = phoneRoot.Find("XScreen");
            if (witchformAppButton == null) witchformAppButton = FindButton("HomeScreen/WitchformAppButton");
            if (xAppButton == null) xAppButton = FindButton("HomeScreen/XAppButton");
            if (homeButton == null) homeButton = FindButton("NavBar/HomeButton");
            if (backButton == null) backButton = FindButton("NavBar/BackButton");
            if (xScreen != null && xScreen.GetComponent<PhonePointerGuard>() == null)
                xScreen.gameObject.AddComponent<PhonePointerGuard>();
            WirePhone();
        }

        private UnityEngine.UI.Button FindButton(string path)
        {
            var child = phoneRoot.Find(path);
            return child != null ? child.GetComponent<UnityEngine.UI.Button>() : null;
        }

        private void WirePhone()
        {
            UnwirePhone();
            if (witchformAppButton != null) witchformAppButton.onClick.AddListener(OpenWitchform);
            if (xAppButton != null) xAppButton.onClick.AddListener(OpenX);
            if (homeButton != null) homeButton.onClick.AddListener(ShowHome);
            if (backButton != null) backButton.onClick.AddListener(GoBack);
        }

        private void UnwirePhone()
        {
            if (witchformAppButton != null) witchformAppButton.onClick.RemoveListener(OpenWitchform);
            if (xAppButton != null) xAppButton.onClick.RemoveListener(OpenX);
            if (homeButton != null) homeButton.onClick.RemoveListener(ShowHome);
            if (backButton != null) backButton.onClick.RemoveListener(GoBack);
        }

        private bool CanUsePhone => expanded && inputEnabled && phoneRoot != null && phoneRoot.gameObject.activeInHierarchy;
        private void OpenWitchform() { if (CanUsePhone) ShowScreen(PhoneScreen.Witchform); }
        private void OpenX() { if (CanUsePhone) ShowScreen(PhoneScreen.X); }
        private void GoBack() { if (CanUsePhone) ShowHome(); }
        private void ShowHome() => ShowScreen(PhoneScreen.Home);

        private void ShowScreen(PhoneScreen screen)
        {
            currentScreen = screen;
            if (homeScreen != null) homeScreen.gameObject.SetActive(screen == PhoneScreen.Home);
            if (witchformScreen != null) witchformScreen.gameObject.SetActive(screen == PhoneScreen.Witchform);
            if (xScreen != null) xScreen.gameObject.SetActive(screen == PhoneScreen.X);
            RefreshPhoneButtons();
        }

        /// <summary>확대 상태와 외부 포커스 입력 허용을 함께 검사한다. 책상 위 자식 그래픽은 레이캐스트를 가리지 않는다.</summary>
        public void SetInputEnabled(bool value)
        {
            if (phoneRoot == null) ResolvePhone();
            bool canUse = expanded && value && phoneRoot != null && phoneRoot.gameObject.activeInHierarchy;
            if (inputEnabled == value && phoneInput != null &&
                phoneInput.interactable == canUse && phoneInput.blocksRaycasts == canUse) return;
            inputEnabled = value;
            if (phoneInput != null)
            {
                phoneInput.interactable = canUse;
                phoneInput.blocksRaycasts = canUse;
            }
            RefreshPhoneButtons();
        }

        private void RefreshPhoneButtons()
        {
            bool canUse = CanUsePhone;
            if (witchformAppButton != null) witchformAppButton.interactable = canUse;
            if (xAppButton != null) xAppButton.interactable = canUse;
            if (homeButton != null) homeButton.interactable = canUse && currentScreen != PhoneScreen.Home;
            if (backButton != null) backButton.interactable = canUse && currentScreen != PhoneScreen.Home;
        }

        /// <summary>복제 문서를 만들지 않고 현재 방문자의 내용을 같은 인스턴스에 다시 표시한다.</summary>
        public void SetExpanded(bool value)
        {
            expanded = value;
            ResolvePhone();
            ShowHome();
            if (!value) SetInputEnabled(false);
            RefreshContent();
        }

        /// <summary>주문 정보를 표시하고, 누락된 데이터는 이전 내용이 남지 않도록 비운다.</summary>
        public void Bind(InspectionNpcData npc)
        {
            boundNpc = npc;
            ResolvePhone();
            ShowHome();
            if (npc == null || npc.order == null) { Clear(); return; }
            RefreshContent();
        }
        private void RefreshContent()
        {
            var npc = boundNpc;
            if (npc == null || npc.order == null) return;
            var data = npc.order;
            if (HasAuthoredLayout)
            {
                if (content != null) content.text = string.Empty;
                if (customerNameText != null) customerNameText.text = npc.identity != null ? npc.identity.displayName : string.Empty;
                if (customerCodeText != null) customerCodeText.text = data.customerCode;
                if (orderNumberText != null) orderNumberText.text = data.orderNumber;
                if (productText != null) productText.text = data.productName;
                if (quantityText != null) quantityText.text = data.quantity.ToString();
            }
            else if (content != null) content.text = expanded
                ? "주문서\n\n주문 번호  " + data.orderNumber + "\n\n고객 코드  " + data.customerCode +
                  "\n\n상품  " + data.productName + "\n수량  " + data.quantity
                : "주문서\n" + data.orderNumber + "\n" + data.customerCode;
        }

        /// <summary>문서 내용과 재표시용 참조를 함께 해제한다.</summary>
        [Button] public void Clear()
        {
            boundNpc = null;
            ResolvePhone();
            ShowHome();
            SetInputEnabled(false);
            if (content != null) content.text = string.Empty;
            if (customerNameText != null) customerNameText.text = string.Empty;
            if (customerCodeText != null) customerCodeText.text = string.Empty;
            if (orderNumberText != null) orderNumberText.text = string.Empty;
            if (productText != null) productText.text = string.Empty;
            if (quantityText != null) quantityText.text = string.Empty;
        }
    }

    // X 피드의 ScrollRect가 드래그를 처리할 때 부모 책상 물품이 pointer down을 선점하지 않도록 한다.
    public sealed class PhonePointerGuard : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData eventData) { }
    }
}
