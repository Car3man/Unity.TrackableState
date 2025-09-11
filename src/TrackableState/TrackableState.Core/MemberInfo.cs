namespace Klopoff.TrackableState.Core
{
    public readonly struct MemberInfo
    {
        public readonly int Id;
        public readonly string Name;

        public MemberInfo(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}