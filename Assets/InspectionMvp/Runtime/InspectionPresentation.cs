using System.Collections;
using UnityEngine;

namespace SheIsNotHuman.InspectionMvp
{
    /// <summary>Presentation operations are awaited by the sole flow authority.</summary>
    public abstract class InspectionPresentation : MonoBehaviour
    {
        public abstract bool IsDialogueTyping { get; }
        public abstract bool IsModalOpen { get; }
        public abstract bool IsModalBusy { get; }
        public abstract float ReactionHoldSeconds { get; }

        public abstract void Initialize(InspectionFlowController owner);
        public abstract void ResetPresentation();
        public abstract void BindNpc(InspectionNpcData npc);
        public abstract void ShowDialogue(string text);
        public abstract void CompleteDialogue();
        public abstract void SetHint(string text);
        public abstract void SetState(InspectionState state);
        public abstract IEnumerator EnterNpc(bool animate);
        public abstract IEnumerator HandoffItems(bool animate);
        public abstract IEnumerator SetInspectionOpen(bool open, bool animate);
        public abstract IEnumerator ResolveDocuments(InspectionDecision decision, bool animate);
        public abstract IEnumerator ExitNpc(bool animate);
        public abstract void ShowCompleted();
        public abstract void CancelActivity();
    }
}
