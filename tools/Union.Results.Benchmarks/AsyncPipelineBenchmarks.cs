using BenchmarkDotNet.Configs;

namespace Union.Results.Benchmarks;

/// <summary>
/// The fluent <c>Task&lt;Result&gt;</c> API compared with hand-written awaits returning <c>Task</c> and
/// <c>ValueTask</c>. The inner operations either complete synchronously (e.g. a cache hit) or
/// genuinely yield (e.g. I/O), and either succeed or fail.
/// </summary>
/// <remarks>
/// The synchronous inner tasks are cached, so allocations reported in the <c>Sync</c> rows come
/// from the pipeline itself rather than from the operations being composed.
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
public class AsyncPipelineBenchmarks
{
    private static readonly Error Missing = new NotFound(0);

    private Task<Result<int>> _cachedFind = null!;
    private Task<Result<int>> _cachedLoad = null!;
    private Func<int, Task<Result<int>>> _load = null!;
    private Task<int> _cachedTwo = null!;
    private Func<int, Task<int>> _addOne = null!;
    private Func<int, Task> _audit = null!;
    private int _id;

    [Params(Completion.Sync, Completion.Async)]
    public Completion Completion { get; set; }

    [Params(Outcome.Ok, Outcome.Err)]
    public Outcome Outcome { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _id = Outcome == Outcome.Ok ? 1 : -1;
        _cachedFind = Task.FromResult(Lookup(_id));
        _cachedLoad = Task.FromResult<Result<int>>(10);
        _load = LoadAsync;
        _cachedTwo = Task.FromResult(2);
        _addOne = AddOneAsync;
        _audit = AuditAsync;
    }

    [Benchmark(Baseline = true)]
    public async Task<int> HandWritten_Task()
    {
        var found = await FindAsync(_id);
        if (!found.TryGetOk(out var id))
        {
            return -1;
        }

        var loaded = await LoadAsync(id + 1);
        return loaded.TryGetOk(out var value) ? value : -1;
    }

    [Benchmark]
    public async ValueTask<int> HandWritten_ValueTask()
    {
        var found = await FindValueAsync(_id);
        if (!found.TryGetOk(out var id))
        {
            return -1;
        }

        var loaded = await LoadValueAsync(id + 1);
        return loaded.TryGetOk(out var value) ? value : -1;
    }

    [Benchmark]
    public Task<int> Fluent_Task() =>
        FindAsync(_id)
            .Map(static x => x + 1)
            .BindAsync(_load)
            .Match(static x => x, static _ => -1);

    [Benchmark]
    public Task<int> Fluent_Task_AsyncSteps() =>
        FindAsync(_id)
            .MapAsync(_addOne)
            .TapAsync(_audit)
            .BindAsync(_load)
            .Match(static x => x, static _ => -1);

    private static Result<int> Lookup(int id) => id > 0 ? id : Missing;

    private Task<int> AddOneAsync(int value) =>
        Completion == Completion.Sync ? _cachedTwo : AddOneSlowAsync(value);

    private Task AuditAsync(int value) =>
        Completion == Completion.Sync ? Task.CompletedTask : AuditSlowAsync();

    private static async Task AuditSlowAsync() => await Task.Yield();

    private static async Task<int> AddOneSlowAsync(int value)
    {
        await Task.Yield();
        return value + 1;
    }

    private Task<Result<int>> FindAsync(int id) =>
        Completion == Completion.Sync ? _cachedFind : FindSlowAsync(id);

    private Task<Result<int>> LoadAsync(int id) =>
        Completion == Completion.Sync ? _cachedLoad : LoadSlowAsync(id);

    private ValueTask<Result<int>> FindValueAsync(int id) =>
        Completion == Completion.Sync ? new(Lookup(id)) : new(FindSlowAsync(id));

    private ValueTask<Result<int>> LoadValueAsync(int id) =>
        Completion == Completion.Sync ? new((Result<int>)10) : new(LoadSlowAsync(id));

    private static async Task<Result<int>> FindSlowAsync(int id)
    {
        await Task.Yield();
        return Lookup(id);
    }

    private static async Task<Result<int>> LoadSlowAsync(int id)
    {
        await Task.Yield();
        return id * 10;
    }
}
