using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using Benchmarks.Models;
using Klopoff.TrackableState.Core;
using TrackableState.Packer;

namespace Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net90, launchCount: 1, warmupCount: 3, iterationCount: 12)]
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    public class TrackableAddCoalescingBenchmarks
    {
        private const int N = 1_000_000;

        [Params(16, 64, 256)]
        public int ScanLimit { get; set; }

        private Consumer _consumer;
        private string[] _values;

        private SampleRoot _plain;
        private TrackableSampleRoot _trackable;
        private ChangeLogBuffer _buffer;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _consumer = new Consumer();
            _values = Enumerable.Range(0, Math.Max(1, N)).Select(i => "v" + i).ToArray();

            _plain = new SampleRoot
            {
                Tags = new List<string>(),
                Inner = new SampleInner()
            };

            _trackable = _plain.AsTrackable();
            _trackable.Changed += OnChange;
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _buffer = new ChangeLogBuffer();
            
            _plain.Name = "N0";
            _plain.Age = 0;
            _plain.Inner = new SampleInner { Description = "init" };
            _plain.Tags.Clear();

            _trackable.Name = "N0";
            _trackable.Age = 0;
            _trackable.Inner = new SampleInner { Description = "init" };
            _trackable.Tags.Clear();

            _trackable.AcceptChanges();
            _buffer.Clear();
        }
        
        private void OnChange(object _, in ChangeEventArgs e) => _buffer.AddCoalescing(e, ScanLimit);
        
        [Benchmark(Description = "AddCoalescing: Sequential Name sets")]
        public void AddCoalescing_NameSequential()
        {
            for (int i = 0; i < N; i++)
            {
                _trackable.Name = _values[i & (_values.Length - 1)];
            }
            
            _consumer.Consume(_trackable);
            _consumer.Consume(_buffer.Count);
        }
        
        [Benchmark(Description = "AddCoalescing: Interleaved Name/Age")]
        public void AddCoalescing_Interleaved_Unrelated_Paths()
        {
            for (int i = 0; i < N; i++)
            {
                if ((i & 1) == 0)
                {
                    _trackable.Name = _values[i & (_values.Length - 1)];
                }
                else
                {
                    _trackable.Age = i;
                }
            }

            _consumer.Consume(_trackable);
            _consumer.Consume(_buffer.Count);
        }
        
        [Benchmark(Description = "AddCoalescing: Ancestor/Descendant barriers (Inner vs Inner.Description)")]
        public void AddCoalescing_With_AncestorDescendant_Barriers()
        {
            const int barrierPeriod = 10_000;

            for (int i = 0; i < N; i++)
            {
                if (i % barrierPeriod == 0)
                {
                    _trackable.Inner = new SampleInner { Description = "init" };
                }
                else
                {
                    _trackable.Inner.Description = _values[i & (_values.Length - 1)];
                }
            }

            _consumer.Consume(_trackable);
            _consumer.Consume(_buffer.Count);
        }
    }
}