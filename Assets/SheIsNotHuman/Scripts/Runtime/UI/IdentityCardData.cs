using System;
using UnityEngine;

namespace WhaleShark.UI
{
    [Serializable]
    public sealed class IdentityCardData
    {
        public string displayName;
        public string customerCode;
        public string footer;
        public Color portraitColor = Color.white;
    }
}
