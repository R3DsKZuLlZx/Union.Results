namespace Union.Results.Tests;

public sealed record NotFound(string Id) : Error("tests.not_found", $"'{Id}' was not found.");

public sealed record Invalid(string Field) : Error("tests.invalid", $"'{Field}' is invalid.");

public sealed record OutOfStock(string Sku) : IError
{
    public string Code => "orders.out_of_stock";

    public string Message => $"'{Sku}' is out of stock.";
}

public sealed record PaymentDeclined(decimal Amount) : IError
{
    public string Code => "orders.payment_declined";

    public string Message => $"A payment of {Amount} was declined.";
}

/// <summary>A closed set of domain errors, written the way the README recommends.</summary>
public union OrderError(OutOfStock, PaymentDeclined) : IError
{
    public string Code => this switch
    {
        OutOfStock e => e.Code,
        PaymentDeclined e => e.Code,
    };

    public string Message => this switch
    {
        OutOfStock e => e.Message,
        PaymentDeclined e => e.Message,
    };
}
