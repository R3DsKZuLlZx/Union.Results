namespace Union.Results.Tests;

public class ResultOfTErrorTests
{
    private static Result<int, string> Parse(string text) =>
        int.TryParse(text, out var number) ? number : $"'{text}' is not a number";

    [Fact]
    public void Implicit_conversions_create_the_expected_case()
    {
        Assert.True(Parse("42").IsOk);
        Assert.True(Parse("x").IsErr);
    }

    [Fact]
    public void Case_types_convert_to_results_even_when_T_and_TError_match()
    {
        Result<string, string> ok = Result.Ok("value");
        Result<string, string> err = Result.Err("error");

        Assert.Equal("value", ok.GetValueOrThrow());
        Assert.Equal("error", err.GetErrorOrThrow());
    }

    [Fact]
    public void Switch_expression_over_cases_is_exhaustive()
    {
        // Compiles without a discard arm; TreatWarningsAsErrors would fail the build on CS8509.
        static string Describe(Result<int, string> result) => result switch
        {
            Ok<int> ok => $"ok {ok.Value}",
            Err<string> err => $"err {err.Error}",
        };

        Assert.Equal("ok 1", Describe(1));
        Assert.Equal("err no", Describe("no"));
    }

    [Fact]
    public void Positional_and_property_patterns_work()
    {
        Result<int, string> result = 5;

        Assert.True(result is Ok<int>(5));
        Assert.True(result is Ok<int> { Value: > 3 });
        Assert.False(result is Err<string>);
    }

    [Fact]
    public void Default_result_is_neither_case_and_throws_on_use()
    {
        var result = default(Result<int, string>);

        Assert.False(result.IsOk);
        Assert.False(result.IsErr);
        Assert.Throws<InvalidOperationException>(() => result.Map(static x => x + 1));
        Assert.Throws<InvalidOperationException>(() => result.Match(static x => x, static _ => 0));
        Assert.Throws<System.Runtime.CompilerServices.SwitchExpressionException>(() => result switch
        {
            Ok<int> => 1,
            Err<string> => 2,
        });
    }

    [Fact]
    public void Map_transforms_values_and_passes_errors_through()
    {
        Assert.Equal(84, Parse("42").Map(static x => x * 2).GetValueOrThrow());
        Assert.Equal("'x' is not a number", Parse("x").Map(static x => x * 2).GetErrorOrThrow());
    }

    [Fact]
    public void MapError_transforms_errors_and_passes_values_through()
    {
        Assert.Equal(42, Parse("42").MapError(static e => e.Length).GetValueOrThrow());
        Assert.Equal(19, Parse("x").MapError(static e => e.Length).GetErrorOrThrow());
    }

    [Fact]
    public void Bind_short_circuits_on_error()
    {
        var calls = 0;
        Result<int, string> Half(int x)
        {
            calls++;
            return x % 2 == 0 ? x / 2 : "odd";
        }

        Assert.Equal(21, Parse("42").Bind(Half).GetValueOrThrow());
        Assert.Equal("odd", Parse("43").Bind(Half).GetErrorOrThrow());
        Assert.True(Parse("x").Bind(Half).IsErr);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Ensure_turns_failing_values_into_errors()
    {
        Assert.Equal("negative", Parse("-1").Ensure(static x => x >= 0, "negative").GetErrorOrThrow());
        Assert.Equal("-1 < 0", Parse("-1").Ensure(static x => x >= 0, static x => $"{x} < 0").GetErrorOrThrow());
        Assert.Equal(1, Parse("1").Ensure(static x => x >= 0, "negative").GetValueOrThrow());
    }

    [Fact]
    public void OrElse_recovers_from_errors()
    {
        Assert.Equal(0, Parse("x").OrElse(static _ => 0).GetValueOrThrow());
        Assert.Equal(7, Parse("7").OrElse(static _ => 0).GetValueOrThrow());
    }

    [Fact]
    public void Tap_and_TapError_run_only_for_their_case()
    {
        var log = new List<string>();

        Parse("1").Tap(x => log.Add($"ok {x}")).TapError(e => log.Add("err"));
        Parse("x").Tap(x => log.Add("ok")).TapError(e => log.Add("err"));

        Assert.Equal(["ok 1", "err"], log);
    }

    [Fact]
    public void Value_accessors_behave_per_case()
    {
        Assert.True(Parse("1").TryGetOk(out var value));
        Assert.Equal(1, value);
        Assert.False(Parse("1").TryGetErr(out _));
        Assert.True(Parse("x").TryGetErr(out var error));
        Assert.Equal("'x' is not a number", error);

        Assert.Equal(9, Parse("x").GetValueOrDefault(9));
        Assert.Equal(19, Parse("x").GetValueOrElse(static e => e.Length));
        Assert.Throws<InvalidOperationException>(() => Parse("x").GetValueOrThrow());
        Assert.Throws<InvalidOperationException>(() => Parse("1").GetErrorOrThrow());
    }

    [Fact]
    public void GetValueOrThrow_preserves_exception_errors_as_inner_exception()
    {
        var cause = new TimeoutException();
        Result<int, Exception> result = cause;

        var thrown = Assert.Throws<InvalidOperationException>(() => result.GetValueOrThrow());
        Assert.Same(cause, thrown.InnerException);
    }

    [Fact]
    public void Linq_query_syntax_composes_results()
    {
        var sum = from a in Parse("1")
                  from b in Parse("2")
                  select a + b;

        var failed = from a in Parse("1")
                     from b in Parse("x")
                     select a + b;

        Assert.Equal(3, sum.GetValueOrThrow());
        Assert.True(failed.IsErr);
    }

    [Fact]
    public void Equality_compares_case_and_payload()
    {
        Assert.Equal(Parse("1"), Parse("1"));
        Assert.NotEqual(Parse("1"), Parse("2"));
        Assert.True(Parse("x") == Parse("x"));
        Assert.True(Parse("1") != Parse("x"));
        Assert.Equal(Parse("1").GetHashCode(), Parse("1").GetHashCode());
        Assert.Equal(default(Result<int, string>), default);
    }

    [Fact]
    public void ToString_describes_the_case()
    {
        Assert.Equal("Ok(1)", Parse("1").ToString());
        Assert.Equal("Err(boom)", ((Result<int, string>)"boom").ToString());
        Assert.Equal("Result.Default", default(Result<int, string>).ToString());
    }

    [Fact]
    public void Sequence_collects_values_or_returns_the_first_error()
    {
        Assert.Equal([1, 2], new[] { Parse("1"), Parse("2") }.Sequence().GetValueOrThrow());
        Assert.Equal("'a' is not a number", new[] { Parse("1"), Parse("a"), Parse("b") }.Sequence().GetErrorOrThrow());
    }

    [Fact]
    public void Try_maps_exceptions_with_the_supplied_function()
    {
        var result = Result.Try<int, string>(static () => throw new FormatException("bad"), static ex => ex.Message);

        Assert.Equal("bad", result.GetErrorOrThrow());
    }
}
