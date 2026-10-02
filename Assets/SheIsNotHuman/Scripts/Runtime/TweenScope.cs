using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace WhaleShark
{
    /// <summary>Owns one unscaled tween and invalidates waiters when cancelled or replaced.</summary>
    public sealed class TweenScope
    {
        private Tween active;
        private int version;
        public void Cancel() { version++; active?.Kill(false); active = null; }
        public IEnumerator Play(Tween tween, GameObject owner)
        {
            Cancel();
            int run = version;
            if (tween == null) yield break;
            if (owner == null || !owner.activeInHierarchy) { tween.Kill(false); yield break; }
            active = tween.SetUpdate(true).SetLink(owner, LinkBehaviour.KillOnDisable);
            while (run == version && tween.IsActive() && !tween.IsComplete()) yield return null;
            if (run == version) active = null;
        }
    }
}
