using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Klopoff.TrackableState.Core;

[Trackable]
public class SampleInner
{
    public virtual string Description { get; set; }
}

[Trackable]
public class SampleRoot
{
    public virtual string Name { get; set; }
    public virtual int Age { get; set; }
    public virtual SampleInner Inner { get; set; }
    public virtual IList<string> Tags { get; set; }
}

namespace Klopoff.TrackableState.TrackablePerformance
{
    [SimpleJob(RuntimeMoniker.Net90, launchCount: 1, warmupCount: 3, iterationCount: 12)]
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    public class TrackablePerformanceBenchmarks
    {
        private const int N = 1_000_000;
        
        private string[] _values;

        private SampleRoot _plain;
        private TrackableSampleRoot _trackable;

        private static int _eventCounter;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _values = Enumerable.Range(0, Math.Max(1, N)).Select(i => "v" + i).ToArray();

            _plain = new SampleRoot
            {
                Tags = new List<string>(),
                Inner = new SampleInner()
            };
            _trackable = _plain.AsTrackable();

            ((ITrackable)_trackable).Changed += static (_, _) => _eventCounter++;
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _eventCounter = 0;

            _plain.Tags.Clear();
            _trackable.Tags.Clear();

            _plain.Inner = new SampleInner { Description = "init" };
            _trackable.Inner = new SampleInner { Description = "init" };

            _plain.Name = "N0";
            _trackable.Name = "N0";

            _plain.Age = 0;
            _trackable.Age = 0;

            ((ITrackable)_trackable).AcceptChanges();
        }

        [Benchmark(Baseline = true, Description = "Plain: property sets (Name/Age)")]
        public (int age, int nameLen) Plain_PropertySets()
        {
            int nameLen = 0;
            for (int i = 0; i < N; i++)
            {
                _plain.Name = _values[i % _values.Length];
                _plain.Age = i;
                nameLen += _plain.Name.Length;
            }
            return (_plain.Age, nameLen);
        }

        [Benchmark(Description = "Trackable: property sets (Name/Age)")]
        public (int age, int nameLen, int ev) Trackable_PropertySets()
        {
            var root = _trackable;
            int nameLen = 0;
            for (int i = 0; i < N; i++)
            {
                root.Name = _values[i % _values.Length];
                root.Age = i;
                nameLen += root.Name.Length;
            }
            return (root.Age, nameLen, _eventCounter);
        }

        [Benchmark(Description = "Plain: Tags.Add N")]
        public (int count, int totalLen) Plain_List_Add()
        {
            int len = 0;
            for (int i = 0; i < N; i++)
            {
                string v = _values[i % _values.Length];
                _plain.Tags.Add(v);
                len += v.Length;
            }
            return (_plain.Tags.Count, len);
        }

        [Benchmark(Description = "Trackable: Tags.Add N")]
        public (int count, int ev, int totalLen) Trackable_List_Add()
        {
            var tags = _trackable.Tags;
            int len = 0;
            for (int i = 0; i < N; i++)
            {
                string v = _values[i % _values.Length];
                tags.Add(v);
                len += v.Length;
            }
            return (tags.Count, _eventCounter, len);
        }
        
        [Benchmark(Description = "Plain: Inner.Description sets")]
        public int Plain_Child_PropertySets()
        {
            int len = 0;
            for (int i = 0; i < N; i++)
            {
                _plain.Inner.Description = _values[i % _values.Length];
                len += _plain.Inner.Description.Length;
            }
            return len;
        }

        [Benchmark(Description = "Trackable: Inner.Description sets")]
        public (int len, int ev) Trackable_Child_PropertySets()
        {
            var root = _trackable;
            int len = 0;
            for (int i = 0; i < N; i++)
            {
                root.Inner.Description = _values[i % _values.Length];
                len += root.Inner.Description.Length;
            }
            return (len, _eventCounter);
        }
    }
}