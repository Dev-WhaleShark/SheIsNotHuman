using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace WhaleShark.Gameplay
{
    /// <summary>방문자 한 명의 대사·서류·반응을 묶는 저작용 에셋이다. 진행 상태는 컨트롤러가 별도로 보관한다.</summary>
    [MovedFrom(true, "SheIsNotHuman.InspectionMvp", null, "InspectionNpcData")]
    [CreateAssetMenu(fileName = "Visitor", menuName = "WhaleShark/Visitor Profile")]
    public sealed class VisitorProfile : ScriptableObject
    {
        [Title("방문자"), LabelText("NPC ID")] public string npcId;
        [LabelText("표시 이름")] public string displayName;
        [LabelText("초상 색상")] public Color portraitColor = Color.white;
        [LabelText("대사"), TextArea(2, 5)] public string[] dialogue = new string[0];
        [Title("검사 서류"), InlineProperty, HideLabel]
        public IdentityData identity = new IdentityData();
        [InlineProperty, HideLabel]
        public OrderData order = new OrderData();
        [Title("판정 반응"), LabelText("PASS 반응"), TextArea(2, 5)] public string passReaction;
        [LabelText("NON PASS 반응"), TextArea(2, 5)] public string nonPassReaction;
    }
}
