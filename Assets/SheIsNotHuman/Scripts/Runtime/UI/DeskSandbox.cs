using R3;
using Sirenix.OdinInspector;
using UnityEngine;

namespace WhaleShark.UI
{
    /// <summary>Sample display/input harness only; no visitor or decision game loop.</summary>
    public sealed class DeskSandbox : MonoBehaviour
    {
        [TabGroup("InspectorTabs","연결"), FoldoutGroup("InspectorTabs/연결/표시",Expanded=false), Required, SerializeField] private ServiceDesk desk;
        [FoldoutGroup("InspectorTabs/연결/표시"), Required, SerializeField] private DialoguePlayer dialogue;
        private CompositeDisposable subscriptions;
        [ShowInInspector, ReadOnly, PropertyOrder(-10), LabelText("최근 UI 요청")] public string LastRequest { get; private set; } = "대기";
        public void Configure(ServiceDesk value,DialoguePlayer player) { desk=value; dialogue=player; }
        private void OnEnable()
        {
            subscriptions=new CompositeDisposable();
            if(desk==null) return;
            desk.DecisionRequested.Subscribe(value=>{ LastRequest=value.ToString(); StartCoroutine(desk.SetFocusOpen(false,true)); }).AddTo(subscriptions);
            desk.RestartRequested.Subscribe(_=>ResetSamples()).AddTo(subscriptions);
        }
        private void Start() => ResetSamples();
        private void OnDisable() { subscriptions?.Dispose(); subscriptions=null; dialogue?.Cancel(); desk?.CancelActivity(); }
        [TabGroup("InspectorTabs","개발 도구"), Button("샘플 표시 초기화"), DisableInEditorMode]
        public void ResetSamples()
        {
            if(!Application.isPlaying || desk==null) return;
            dialogue?.Cancel(); desk.ResetPresentation();
            desk.Bind(new IdentityCardData{displayName="김서윤",customerCode="C-101",footer="NPC_01",portraitColor=new Color(.55f,.61f,.62f)},
                new OrderDisplayData{customerName="김서윤",customerCode="C-101",orderNumber="ORD-001",productName="식량 상자",quantity=1});
            desk.SetMode(DeskMode.Review); desk.SetProgress(0); desk.SetHint("물품을 클릭하여 검사");
            StartCoroutine(desk.HandoffItems(false)); LastRequest="샘플 표시";
        }
        [TabGroup("InspectorTabs","개발 도구"), Button("독립 대사 재생"), DisableInEditorMode]
        public void PlayDialogue()
        {
            if(!Application.isPlaying || dialogue==null || desk==null) return;
            desk.CancelActivity(); desk.SetMode(DeskMode.Dialogue);
            dialogue.Play(new[]{"주문 정보를 확인해 주세요.","감사합니다."});
        }
    }
}
