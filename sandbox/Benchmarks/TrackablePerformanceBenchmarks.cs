using System.Runtime.CompilerServices;
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
        private static int _counter;

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
            _trackable.Changed += OnChange;

            static void OnChange(object o, in ChangeEventArgs changeEventArgs) => _counter++;
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _plain.Name = "N0";
            _plain.Age = 0;
            _plain.Inner = new SampleInner { Description = "init" };
            _plain.Tags.Clear();

            _trackable.Name = "N0";
            _trackable.Age = 0;
            _trackable.Inner = new SampleInner { Description = "init" };
            _trackable.Tags.Clear();

            _trackable.AcceptChanges();
            _counter = 0;
        }

        [Benchmark(Baseline = true, Description = "Plain: Age sets (value-type)")]
        public void Plain_AgePropertySets()
        {
            for (int i = 0; i < N; i++)
            {
                _plain.Age = i;
            }
        }

        [Benchmark(Description = "Trackable: Age sets (value-type)")]
        public void Trackable_AgePropertySets()
        {
            for (int i = 0; i < N; i++)
            {
                _trackable.Age = i;
            }
        }
        
        [Benchmark(Description = "Plain: Name sets")]
        public void Plain_NamePropertySets()
        {
            for (int i = 0; i < N; i++)
            {
                _plain.Name = _values[i % _values.Length];
            }
        }

        [Benchmark(Description = "Trackable: Name sets")]
        public void Trackable_NamePropertySets()
        {
            for (int i = 0; i < N; i++)
            {
                _trackable.Name = _values[i % _values.Length];
            }
        }

        [Benchmark(Description = "Plain: Tags.Add N")]
        public void Plain_List_Add()
        {
            for (int i = 0; i < N; i++)
            {
                _plain.Tags.Add(_values[i % _values.Length]);
            }
        }

        [Benchmark(Description = "Trackable: Tags.Add N")]
        public void Trackable_List_Add()
        {
            for (int i = 0; i < N; i++)
            {
                _trackable.Tags.Add(_values[i % _values.Length]);
            }
        }
        
        [Benchmark(Description = "Plain: Inner.Description sets")]
        public void Plain_Child_PropertySets()
        {
            for (int i = 0; i < N; i++)
            {
                _plain.Inner.Description = _values[i % _values.Length];
            }
        }

        [Benchmark(Description = "Trackable: Inner.Description set")]
        public void Trackable_Child_PropertySets()
        {
            for (int i = 0; i < N; i++)
            {
                _trackable.Inner.Description = _values[i % _values.Length];
            }
        }
    }
}