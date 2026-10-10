using System.Runtime.CompilerServices;

namespace Union.Results.Tests;

/// <summary>
/// Guards the "no allocations" promise for the common paths. Each scenario is run once to JIT it,
/// then measured on the current thread.
/// </summary>
public class AllocationTests
{
    private static readonly Error CachedError = new NotFound("x");

    [Fact]
    public void Creating_and_matching_results_does_not_allocate()
    {
        AssertNoAllocations(static () =>
        {
            Result<int, string> ok = 1;
            Result<int> err = CachedError;
            var total = ok switch { Ok<int> o => o.Value, Err<string> => 0 };
            total += err switch { Ok<int> o => o.Value, Err<Error> => 1 };
            return total;
        });
    }

    [Fact]
    public void Static_lambda_pipelines_do_not_allocate()
    {
        AssertNoAllocations(static () =>
        {
            Result<int> result = 20;
            return result
                .Map(static x => x + 1)
                .Ensure(static x => x > 0, CachedError)
                .Bind(static x => (Result<int>)(x * 2))
                .Match(static x => x, static _ => -1);
        });
    }

    [Fact]
    public void Linq_query_does_not_allocate_beyond_its_lambdas()
    {
        AssertNoAllocations(static () =>
        {
            Result<int, string> a = 1;
            var sum = from x in a
                      from y in (Result<int, string>)2
                      select x + y;
            return sum.GetValueOrThrow();
        });
    }

    private static void AssertNoAllocations(Func<int> scenario)
    {
        Run(scenario);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Run(scenario);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run(Func<int> scenario)
    {
        for (var i = 0; i < 100; i++)
        {
            scenario();
        }
    }
}
