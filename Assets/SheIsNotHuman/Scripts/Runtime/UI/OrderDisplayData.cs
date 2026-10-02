using System;

namespace WhaleShark.UI
{
    [Serializable]
    public sealed class OrderDisplayData
    {
        public string customerName;
        public string customerCode;
        public string orderNumber;
        public string productName;
        public int quantity;
    }
}
