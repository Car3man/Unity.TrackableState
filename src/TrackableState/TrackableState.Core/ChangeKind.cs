namespace Klopoff.TrackableState.Core
{
    public enum ChangeKind : byte
    {
        None = 0,
        PropertySet,
        CollectionAdd,
        CollectionRemove,
        CollectionReplace,
        CollectionClear,
        ChildChange
    }
}