using Klopoff.TrackableState.Core;

namespace TrackableState.Packer
{
    public readonly struct PathKey
    {
        public readonly FixedList8<PathSegment> Path;
        public readonly int Hash;

        public PathKey(in FixedList8<PathSegment> path)
        {
            Path = path;
            
            unchecked
            {
                int h = 17;
                
                for (int i = 0; i < Path.Count; i++)
                {
                    h = h * 31 + Path[i].segmentType.GetHashCode();
                    h = h * 31 + Path[i].memberInfo.Id;
                }
                
                Hash = h;
            }
        }
    }
}