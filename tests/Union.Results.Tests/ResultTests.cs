namespace Union.Results.Tests;

public class ResultTests
{
    private static Result Save(string name) => name.Length > 0 ? Result.Ok() : new Invalid(nameof(name));

    [Fact]
    public void Ok_and_errors_convert_implicitly()
    {
        Assert.True(Save("order").IsOk);
        Assert.Equal(new Invalid("name"), Save("").GetErrorOrThrow());
    }

    [Fact]
    public void Switch_expression_over_cases_is_exhaustive()
    {
        static string Describe(Result result) => result switch
        {
            Ok => "saved",
            Err<Error>(Invalid { Field: var field }) => $"invalid {field}",
            Err<Error>(var other) => other.Message,
        };

        Assert.Equal("saved", Describe(Save("order")));
        Assert.Equal("invalid name", Describe(Save("")));
        Assert.Equal("'x' was not found.", Describe(new NotFound("x")));
    }

    [Fact]
    public void Default_result_is_neither_case_and_throws_on_use()
    {
        var result = default(Result);

        Assert.False(result.IsOk);
        Assert.False(result.IsErr);
        Assert.Throws<InvalidOperationException>(() => result.ThrowIfErr());
        Assert.Throws<InvalidOperationException>(() => result.Map(static () => 1));
        Assert.Throws<System.Runtime.CompilerServices.SwitchExpressionException>(() => result switch
        {
            Ok => 1,
            Err<Error> => 2,
        });
    }

    [Fact]
    public void Null_errors_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new Result(new Err<Error>(null!)));
    }

    [Fact]
    public void Error_accessors_behave_per_case()
    {
        Assert.True(Save("").TryGetErr(out var error));
        Assert.Equal(new Invalid("name"), error);
        Assert.False(Save("order").TryGetErr(out _));

        Save("order").ThrowIfErr();
        Assert.Throws<InvalidOperationException>(() => Save("").ThrowIfErr());
        Assert.Throws<InvalidOperationException>(() => Save("order").GetErrorOrThrow());
    }

    [Fact]
    public void Map_produces_a_value_after_success()
    {
        Assert.Equal(42, Save("order").Map(static () => 42).GetValueOrThrow());
        Assert.IsType<Invalid>(Save("").Map(static () => 42).GetErrorOrThrow());
    }

    [Fact]
    public void Bind_chains_operations_with_and_without_values()
    {
        Assert.True(Save("a").Bind(static () => Save("b")).IsOk);
        Assert.True(Save("a").Bind(static () => Save("")).IsErr);
        Assert.Equal(7, Save("a").Bind(static () => (Result<int>)7).GetValueOrThrow());
        Assert.IsType<Invalid>(Save("").Bind(static () => (Result<int>)7).GetErrorOrThrow());
    }

    [Fact]
    public void Ensure_MapError_OrElse_Tap_and_Match_work()
    {
        var log = new List<string>();

        var result = Save("order")
            .Ensure(static () => false, new Invalid("state"))
            .MapError(static e => e.WithMetadata("attempt", 1))
            .TapError(e => log.Add(e.Code))
            .OrElse(static _ => Result.Ok())
            .Tap(() => log.Add("recovered"));

        Assert.True(result.IsOk);
        Assert.Equal(["tests.invalid", "recovered"], log);
        Assert.Equal("ok", result.Match(static () => "ok", static e => e.Code));
        Assert.True(Save("x").Ensure(static () => false, static () => new Invalid("lazy")).IsErr);
    }

    [Fact]
    public void Switch_runs_the_matching_branch()
    {
        var outcome = "";
        Save("").Switch(() => outcome = "ok", e => outcome = e.Code);

        Assert.Equal("tests.invalid", outcome);
    }

    [Fact]
    public void Equality_and_ToString()
    {
        Assert.Equal(Save("a"), Save("b"));
        Assert.Equal(Save(""), Save(""));
        Assert.NotEqual(Save("a"), Save(""));
        Assert.True(Save("a") == Result.Ok());
        Assert.Equal("Ok", Save("a").ToString());
        Assert.StartsWith("Err(Invalid", Save("").ToString());
        Assert.Equal("Result.Default", default(Result).ToString());
    }

    [Fact]
    public void Try_captures_exceptions_but_not_cancellation()
    {
        Assert.True(Result.Try(static () => { }).IsOk);
        Assert.IsType<ExceptionError>(Result.Try(static () => throw new IOException()).GetErrorOrThrow());
        Assert.Throws<OperationCanceledException>(() => Result.Try(static () => throw new OperationCanceledException()));
    }

    [Fact]
    public void Sequence_returns_first_error_and_Combine_returns_all_errors()
    {
        Result[] results = [Save("a"), new Invalid("x"), Save("b"), new Invalid("y")];

        Assert.Equal(new Invalid("x"), results.Sequence().GetErrorOrThrow());
        var aggregate = Assert.IsType<AggregateError>(results.Combine().GetErrorOrThrow());
        Assert.Equal([new Invalid("x"), new Invalid("y")], aggregate.Errors);

        Result[] allOk = [Save("a"), Save("b")];
        Assert.True(allOk.Sequence().IsOk);
        Assert.True(allOk.Combine().IsOk);
    }

    [Fact]
    public void Ok_serves_as_the_value_for_general_results_without_values()
    {
        static Result<Ok, OrderError> Cancel(bool paid) =>
            paid ? Result.Err<OrderError>(new PaymentDeclined(10)) : Result.Ok();

        Assert.True(Cancel(paid: false).IsOk);
        Assert.Equal("orders.payment_declined", Cancel(paid: true).GetErrorOrThrow().Code);
        Assert.Equal("cancelled", Cancel(paid: false) switch
        {
            Ok<Ok> => "cancelled",
            Err<OrderError> => "failed",
        });
    }

    [Fact]
    public async Task Async_members_compose()
    {
        static async Task<Result> SaveAsync(string name)
        {
            await Task.Yield();
            return Save(name);
        }

        var log = new List<string>();

        var count = await SaveAsync("a")
            .Ensure(static () => true, new Invalid("never"))
            .TapAsync(async () => { await Task.Yield(); log.Add("saved"); })
            .BindAsync(static () => SaveAsync("b"))
            .MapAsync(static async () => { await Task.Yield(); return 2; });

        Assert.Equal(2, count.GetValueOrThrow());
        Assert.Equal(["saved"], log);

        var failed = await SaveAsync("")
            .BindAsync(() => { log.Add("not called"); return SaveAsync("b"); })
            .Match(static () => "ok", static e => e.Code);

        Assert.Equal("tests.invalid", failed);
        Assert.Equal(["saved"], log);
    }

    [Fact]
    public async Task Value_results_bind_into_async_operations_without_values()
    {
        static Task<Result<int>> FindAsync(bool found) =>
            Task.FromResult<Result<int>>(found ? 1 : new NotFound("x"));

        Assert.True((await FindAsync(true).BindAsync(static x => Task.FromResult(Save($"{x}")))).IsOk);
        Assert.IsType<NotFound>((await FindAsync(false).Bind(static x => Save($"{x}"))).GetErrorOrThrow());
    }

    [Fact]
    public async Task TryAsync_captures_exceptions_but_not_cancellation()
    {
        Assert.True((await Result.TryAsync(static () => Task.CompletedTask)).IsOk);
        Assert.IsType<ExceptionError>((await Result.TryAsync(static () => Task.FromException(new IOException()))).GetErrorOrThrow());
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            Result.TryAsync(static () => Task.FromCanceled(new CancellationToken(canceled: true))));
    }

    [Fact]
    public async Task Async_steps_on_default_results_fault()
    {
        var pending = Task.FromResult(default(Result));

        await Assert.ThrowsAsync<InvalidOperationException>(() => pending.BindAsync(static () => Task.FromResult<Result>(Result.Ok())));
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending.TapAsync(static () => Task.CompletedTask));
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending.MapAsync(static () => Task.FromResult(1)));
    }
}
