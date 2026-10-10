namespace Union.Results.Tests;

public class ErrorTests
{
    [Fact]
    public void Errors_have_value_equality_including_metadata_and_cause()
    {
        Assert.Equal(new NotFound("a"), new NotFound("a"));
        Assert.NotEqual<Error>(new NotFound("a"), new Invalid("a"));

        Assert.Equal(new NotFound("a").WithMetadata("k", 1), new NotFound("a").WithMetadata("k", 1));
        Assert.NotEqual(new NotFound("a").WithMetadata("k", 1), new NotFound("a").WithMetadata("k", 2));

        Assert.Equal(new NotFound("a").CausedBy(new Invalid("b")), new NotFound("a").CausedBy(new Invalid("b")));
        Assert.NotEqual(new NotFound("a").CausedBy(new Invalid("b")), new NotFound("a"));
    }

    [Fact]
    public void WithMetadata_returns_a_copy_and_preserves_the_derived_type()
    {
        var original = new NotFound("a");
        var tagged = original.WithMetadata("tenant", "t1").WithMetadata("attempt", 2);

        Assert.Empty(original.Metadata);
        var typed = Assert.IsType<NotFound>(tagged);
        Assert.Equal("a", typed.Id);
        Assert.Equal("t1", typed.Metadata["tenant"]);
        Assert.Equal(2, typed.Metadata["attempt"]);
    }

    [Fact]
    public void ToString_is_readable()
    {
        var error = new NotFound("a").WithMetadata("k", 1).CausedBy(new Invalid("b"));

        Assert.Equal(
            "NotFound { Code = tests.not_found, Message = 'a' was not found., Metadata = { k = 1 }, Cause = Invalid { Code = tests.invalid, Message = 'b' is invalid., Field = b }, Id = a }",
            error.ToString());
    }

    [Fact]
    public void AggregateError_requires_errors_and_compares_by_sequence()
    {
        Assert.Throws<ArgumentException>(() => new AggregateError([]));

        var aggregate = new AggregateError([new Invalid("a"), new Invalid("b")]);

        Assert.Equal(AggregateError.ErrorCode, aggregate.Code);
        Assert.Equal("2 error(s) occurred.", aggregate.Message);
        Assert.Equal(aggregate, new AggregateError([new Invalid("a"), new Invalid("b")]));
        Assert.NotEqual(aggregate, new AggregateError([new Invalid("b"), new Invalid("a")]));
        Assert.Contains("Errors = [ Invalid", aggregate.ToString());
    }

    [Fact]
    public void Union_errors_work_with_the_general_result()
    {
        static Result<int, OrderError> Reserve(string sku, int qty) =>
            qty > 2 ? Result.Err<OrderError>(new OutOfStock(sku)) : qty;

        var message = Reserve("apple", 5) switch
        {
            Ok<int> ok => $"reserved {ok.Value}",
            Err<OrderError>(OutOfStock { Sku: var sku }) => $"no {sku}",
            Err<OrderError>(PaymentDeclined) => "declined",
            Err<OrderError>(null) => throw new InvalidOperationException("Uninitialized OrderError."),
        };

        Assert.Equal("no apple", message);
        Assert.Equal("orders.out_of_stock", Reserve("apple", 5).GetErrorOrThrow().Code);
        Assert.Equal(2, Reserve("apple", 2).GetValueOrThrow());
    }

    [Fact]
    public void Union_errors_are_usable_through_IError()
    {
        OrderError error = new PaymentDeclined(9.99m);
        IError described = error;

        Assert.Equal("orders.payment_declined", described.Code);
        Assert.Equal("A payment of 9.99 was declined.", described.Message);
    }
}
