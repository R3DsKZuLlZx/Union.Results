using BenchmarkDotNet.Configs;

namespace Union.Results.Benchmarks;

/// <summary>
/// The cost of producing and consuming a single result, compared with returning a bool + out value
/// and with throwing an exception.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class CreationBenchmarks
{
    private static readonly Error CachedError = new NotFound(0);

    private int _found = 42;
    private int _missing = -1;

    [Benchmark(Baseline = true), BenchmarkCategory("Success")]
    public int Success_TryPattern() => TryFind(_found, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Success")]
    public int Success_ResultOfTError() => FindGeneral(_found) switch
    {
        Ok<int> ok => ok.Value,
        Err<string> => -1,
    };

    [Benchmark, BenchmarkCategory("Success")]
    public int Success_ResultOfT() => FindCached(_found) switch
    {
        Ok<int> ok => ok.Value,
        Err<Error> => -1,
    };

    [Benchmark(Baseline = true), BenchmarkCategory("Failure")]
    public int Failure_TryPattern() => TryFind(_missing, out var value) ? value : -1;

    [Benchmark, BenchmarkCategory("Failure")]
    public int Failure_ResultOfTError() => FindGeneral(_missing) switch
    {
        Ok<int> ok => ok.Value,
        Err<string> => -1,
    };

    [Benchmark, BenchmarkCategory("Failure")]
    public int Failure_ResultOfT_CachedError() => FindCached(_missing) switch
    {
        Ok<int> ok => ok.Value,
        Err<Error> => -1,
    };

    [Benchmark, BenchmarkCategory("Failure")]
    public int Failure_ResultOfT_NewError() => FindNew(_missing) switch
    {
        Ok<int> ok => ok.Value,
        Err<Error> => -1,
    };

    [Benchmark, BenchmarkCategory("Failure")]
    public int Failure_Exception()
    {
        try
        {
            return FindOrThrow(_missing);
        }
        catch (NotFoundException)
        {
            return -1;
        }
    }

    private static bool TryFind(int id, out int value)
    {
        value = id;
        return id > 0;
    }

    private static Result<int, string> FindGeneral(int id) => id > 0 ? id : "not found";

    private static Result<int> FindCached(int id) => id > 0 ? id : CachedError;

    private static Result<int> FindNew(int id) => id > 0 ? id : new NotFound(id);

    private static int FindOrThrow(int id) => id > 0 ? id : throw new NotFoundException(id);
}
