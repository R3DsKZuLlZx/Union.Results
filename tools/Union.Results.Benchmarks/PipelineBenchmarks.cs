using BenchmarkDotNet.Configs;

namespace Union.Results.Benchmarks;

/// <summary>
/// A Map → Ensure → Bind → Match chain on a success and a failure, compared with the equivalent
/// hand-written control flow.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
public class PipelineBenchmarks
{
    private static readonly Error TooSmall = new NotFound(0);
    private static readonly Error Odd = new NotFound(1);

    private int _input;

    [Params(Outcome.Ok, Outcome.Err)]
    public Outcome Outcome { get; set; }

    [GlobalSetup]
    public void Setup() => _input = Outcome == Outcome.Ok ? 20 : -1;

    [Benchmark(Baseline = true)]
    public int HandWritten()
    {
        var value = _input + 1;
        if (value <= 0)
        {
            return -1;
        }

        return value % 2 == 0 ? value / 2 : -1;
    }

    [Benchmark]
    public int Fluent_StaticLambdas() =>
        ((Result<int>)_input)
            .Map(static x => x + 1)
            .Ensure(static x => x > 0, TooSmall)
            .Bind(static x => x % 2 == 0 ? (Result<int>)(x / 2) : Odd)
            .Match(static x => x, static _ => -1);

    [Benchmark]
    public int Fluent_CapturingLambda()
    {
        var offset = _input > 100 ? 2 : 1;
        return ((Result<int>)_input)
            .Map(x => x + offset)
            .Ensure(static x => x > 0, TooSmall)
            .Bind(static x => x % 2 == 0 ? (Result<int>)(x / 2) : Odd)
            .Match(static x => x, static _ => -1);
    }

    [Benchmark]
    public int Linq_QuerySyntax()
    {
        var result = from a in (Result<int>)_input
                     from b in (Result<int>)(a + 1)
                     select a + b;
        return result.Match(static x => x, static _ => -1);
    }
}
