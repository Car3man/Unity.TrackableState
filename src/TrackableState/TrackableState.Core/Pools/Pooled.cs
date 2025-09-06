using System;

namespace Klopoff.TrackableState.Core.Pools
{
    public readonly struct Pooled<TCollection> : IDisposable where TCollection : class
    {
        private readonly TCollection _instance;
        private readonly Action<TCollection> _return;

        public TCollection Instance => _instance;

        internal Pooled(TCollection instance, Action<TCollection> returnAction)
        {
            _instance = instance;
            _return = returnAction;
        }

        public void Dispose()
        {
            _return(_instance);
        }
    }
}