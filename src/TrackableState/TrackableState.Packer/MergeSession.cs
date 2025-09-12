using Klopoff.TrackableState.Core;

namespace TrackableState.Packer
{
    public struct MergeSession
    {
        public int FirstIndex;
        public ChangeEventArgs FirstEvent;
        public ChangeEventArgs LastEvent;
        public int Count;

        public MergeSession(int firstIndex, ChangeEventArgs firstEvent, ChangeEventArgs lastEvent)
        {
            FirstIndex = firstIndex;
            FirstEvent = firstEvent;
            LastEvent = lastEvent;
            Count = 1;
        }

        public void Add(ChangeEventArgs e)
        {
            LastEvent = e;
            Count++;
        }

        public bool NeedsMerge => Count >= 2;
    }
}