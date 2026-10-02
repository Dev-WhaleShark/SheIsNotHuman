using System;
using System.Collections.Generic;

namespace WhaleShark
{
    /// <summary>Independent overlapping input leases; stale leases cannot release new locks.</summary>
    public sealed class InputLock
    {
        private readonly SortedDictionary<long, string> leases = new();
        private long nextId;
        public bool IsLocked => leases.Count != 0;
        public string Reason => string.Join(", ", leases.Values);
        public IDisposable Acquire(string reason)
        {
            long id = checked(++nextId);
            leases.Add(id, string.IsNullOrEmpty(reason) ? "입력 잠금" : reason);
            return new Lease(this, id);
        }
        public void Clear() => leases.Clear();
        private sealed class Lease : IDisposable
        {
            private InputLock owner;
            private readonly long id;
            public Lease(InputLock owner, long id) { this.owner = owner; this.id = id; }
            public void Dispose() { owner?.leases.Remove(id); owner = null; }
        }
    }
}
