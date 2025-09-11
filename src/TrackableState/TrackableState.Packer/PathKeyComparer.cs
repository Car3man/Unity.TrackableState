using System.Collections.Generic;

namespace TrackableState.Packer
{
    public class PathKeyComparer : IEqualityComparer<PathKey>
    {
        public bool Equals(PathKey x, PathKey y)
        {
            if (x.Hash != y.Hash)
            {
                return false;
            }

            if (x.Path.Count != y.Path.Count)
            {
                return false;
            }

            for (int i = 0; i < x.Path.Count; i++)
            {
                if (x.Path[i].segmentType != y.Path[i].segmentType)
                {
                    return false;
                }

                if (x.Path[i].memberInfo.Id != y.Path[i].memberInfo.Id)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        public int GetHashCode(PathKey k) => k.Hash;
    }
}