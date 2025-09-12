using Klopoff.TrackableState.Core;

namespace Benchmarks.Models
{
    [Trackable]
    public class SampleInner
    {
        public virtual string Description { get; set; }
    }
}