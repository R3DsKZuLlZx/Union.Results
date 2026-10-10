namespace Union.Results.Tests;

public class AsyncTests
{
    private static async Task<Result<int>> FindAsync(string id)
    {
        await Task.Yield();
        return id == "1" ? 1 : new NotFound(id);
    }

    private static async Task<Result<string>> DescribeAsync(int value)
    {
        await Task.Yield();
        return $"#{value}";
    }

    [Fact]
    public async Task Task_pipelines_compose_fluently()
    {
        var log = new List<string>();

        var result = await FindAsync("1")
            .Ensure(static x => x > 0, new Invalid("id"))
            .Tap(x => log.Add($"found {x}"))
            .Map(static x => x * 10)
            .BindAsync(DescribeAsync)
            .TapAsync(async s => { await Task.Yield(); log.Add(s); });

        Assert.Equal("#10", result.GetValueOrThrow());
        Assert.Equal(["found 1", "#10"], log);
    }

    [Fact]
    public async Task Task_pipelines_short_circuit_on_error()
    {
        var called = false;

        var message = await FindAsync("2")
            .BindAsync(x => { called = true; return DescribeAsync(x); })
            .Match(static s => s, static e => e.Message);

        Assert.False(called);
        Assert.Equal("'2' was not found.", message);
    }

    [Fact]
    public async Task General_form_task_pipelines_compose()
    {
        static Task<Result<int, string>> Load(bool ok) =>
            Task.FromResult<Result<int, string>>(ok ? 2 : "missing");

        Assert.Equal(4, (await Load(true).Map(static x => x * 2)).GetValueOrThrow());
        Assert.Equal(7, (await Load(false).MapError(static e => e.Length)).GetErrorOrThrow());
        Assert.Equal("ok", await Load(true).MatchAsync(static _ => Task.FromResult("ok"), static _ => Task.FromResult("err")));
    }

    [Fact]
    public async Task MapAsync_and_MatchAsync_work_on_results()
    {
        Result<int> result = 3;

        var mapped = await result.MapAsync(static async x => { await Task.Yield(); return x + 1; });
        var text = await mapped.MatchAsync(static x => Task.FromResult($"{x}"), static _ => Task.FromResult("err"));

        Assert.Equal("4", text);
    }

    [Fact]
    public async Task TryAsync_captures_exceptions_but_not_cancellation()
    {
        var failed = await Result.TryAsync<int>(static () => Task.FromException<int>(new IOException("disk")));
        Assert.IsType<ExceptionError>(failed.GetErrorOrThrow());

        var mapped = await Result.TryAsync<int, string>(static () => Task.FromException<int>(new IOException("disk")), static ex => ex.Message);
        Assert.Equal("disk", mapped.GetErrorOrThrow());

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            Result.TryAsync<int>(static () => Task.FromCanceled<int>(new CancellationToken(canceled: true))));
    }
}
