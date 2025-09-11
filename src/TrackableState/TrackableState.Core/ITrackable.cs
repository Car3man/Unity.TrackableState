namespace Klopoff.TrackableState.Core
{
    public interface ITrackable
    {
        event ChangeEventHandler Changed;
        bool IsDirty { get; }
        void AcceptChanges();
    }
}