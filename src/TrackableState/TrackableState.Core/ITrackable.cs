namespace Klopoff.TrackableState.Core
{
    public interface ITrackable
    {
        event ChangeEventHandler Changed;
        long Version { get; }
        bool IsDirty { get; }
        void AcceptChanges();
    }
}