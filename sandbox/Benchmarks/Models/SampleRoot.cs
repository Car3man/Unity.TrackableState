using Klopoff.TrackableState.Core;

namespace Benchmarks.Models
{
    [Trackable]
    public class SampleRoot
    {
        public virtual string Name { get; set; }
        public virtual int Age { get; set; }
        public virtual SampleInner Inner { get; set; }
        public virtual IList<string> Tags { get; set; }
    }
}