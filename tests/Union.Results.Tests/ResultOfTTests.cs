namespace Union.Results.Tests;

public class ResultOfTTests
{
    private static readonly Dictionary<string, int> Stock = new() { ["apple"] = 3 };

    private static Result<int> FindStock(string sku) =>
        Stock.TryGetValue(sku, out var count) ? count : new NotFound(sku);

    [Fact]
    public void Error_subtypes_convert_implicitly_to_failed_results()
    {
        var result = FindStock("pear");

        Assert.Equal(new NotFound("pear"), result.GetErrorOrThrow());
    }

    [Fact]
    public void Switch_expression_over_cases_is_exhaustive_and_errors_can_be_matched_by_type()
    {
        static string Describe(Result<int> result) => result switch
        {
            Ok<int>(var count) => $"{count} left",
            Err<Error>(NotFound { Id: var id }) => $"no {id}",
            Err<Error>(var other) => other.Message,
        };

        Assert.Equal("3 left", Describe(FindStock("apple")));
        Assert.Equal("no pear", Describe(FindStock("pear")));
        Assert.Equal("'qty' is invalid.", Describe(new Invalid("qty")));
    }

    [Fact]
    public void Converts_to_and_from_the_general_form()
    {
        Result<int, Error> general = FindStock("apple");
        Result<int> specific = general;

        Assert.Equal(3, specific.GetValueOrThrow());
        Assert.True(general.MapError(static e => e.Code).IsOk);
    }

    [Fact]
    public void Default_result_throws_on_use()
    {
        var result = default(Result<int>);

        Assert.False(result.IsOk);
        Assert.False(result.IsErr);
        Assert.Throws<InvalidOperationException>(() => result.Bind(static x => (Result<int>)x));
        Assert.Throws<InvalidOperationException>(() => result.OrElse(static _ => 0));
    }

    [Fact]
    public void Null_errors_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new Result<int>(new Err<Error>(null!)));
    }

    [Fact]
    public void Pipeline_operators_work()
    {
        var result = FindStock("apple")
            .Ensure(static n => n > 5, static n => new Invalid($"stock {n}"))
            .MapError(static e => e.WithMetadata("sku", "apple"))
            .Map(static n => n * 2);

        var error = Assert.IsType<Invalid>(result.GetErrorOrThrow());
        Assert.Equal("apple", error.Metadata["sku"]);
    }

    [Fact]
    public void Bind_chains_and_short_circuits()
    {
        static Result<string> Label(int n) => n > 0 ? $"{n} in stock" : new Invalid("count");

        Assert.Equal("3 in stock", FindStock("apple").Bind(Label).GetValueOrThrow());
        Assert.IsType<NotFound>(FindStock("pear").Bind(Label).GetErrorOrThrow());
    }

    [Fact]
    public void Linq_query_syntax_composes_results()
    {
        Stock["kiwi"] = 4;
        var total = from a in FindStock("apple")
                    from k in FindStock("kiwi")
                    select a + k;

        Assert.Equal(7, total.GetValueOrThrow());
    }

    [Fact]
    public void Bind_can_chain_into_operations_without_values()
    {
        var saved = new List<int>();
        Result Save(int count)
        {
            if (count > 5)
            {
                return new Invalid("count");
            }

            saved.Add(count);
            return Result.Ok();
        }

        Assert.True(FindStock("apple").Bind(Save).IsOk);
        Assert.IsType<NotFound>(FindStock("pear").Bind(Save).GetErrorOrThrow());
        Assert.Equal([3], saved);
    }

    [Fact]
    public void Try_captures_exceptions_as_ExceptionError()
    {
        var failure = new InvalidDataException("corrupt");
        var result = Result.Try<int>(() => throw failure);

        var error = Assert.IsType<ExceptionError>(result.GetErrorOrThrow());
        Assert.Same(failure, error.Exception);
        Assert.Equal(ExceptionError.ErrorCode, error.Code);
        var thrown = Assert.Throws<InvalidOperationException>(() => result.GetValueOrThrow());
        Assert.Same(failure, thrown.InnerException);
    }

    [Fact]
    public void Try_lets_cancellation_propagate()
    {
        Assert.Throws<OperationCanceledException>(() => Result.Try<int>(static () => throw new OperationCanceledException()));
    }

    [Fact]
    public void Sequence_returns_first_error_and_Combine_returns_all_errors()
    {
        Result<int>[] results = [1, new Invalid("a"), 2, new Invalid("b")];

        Assert.Equal(new Invalid("a"), results.Sequence().GetErrorOrThrow());

        var aggregate = Assert.IsType<AggregateError>(results.Combine().GetErrorOrThrow());
        Assert.Equal([new Invalid("a"), new Invalid("b")], aggregate.Errors);
    }

    [Fact]
    public void Combine_returns_a_single_error_unwrapped_and_values_when_all_succeed()
    {
        Result<int>[] oneError = [1, new Invalid("a")];
        Result<int>[] allOk = [1, 2];

        Assert.Equal(new Invalid("a"), oneError.Combine().GetErrorOrThrow());
        Assert.Equal([1, 2], allOk.Combine().GetValueOrThrow());
    }
}
