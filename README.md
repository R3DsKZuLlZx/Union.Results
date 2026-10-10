# Union.Results

An opinionated result type built on C# 15 unions, so the compiler checks that your pattern matches are exhaustive. Common use does not allocate.

One package, one namespace (`Union.Results`):

- **Results**: `Result<T>`, `Result<T, TError>`, `Ok<T>`, `Err<TError>`, `Unit`, and a fluent, async-friendly API.
- **Errors**: building blocks for defining your own errors: `IError`, the `Error` base record, `ExceptionError`, `AggregateError`.

Requires .NET 11 / C# 15.

```
dotnet add package Union.Results
```

## Results

```csharp
public sealed record UserNotFound(Guid Id) : Error("users.not_found", $"User '{Id}' was not found.");

Result<User> Find(Guid id) =>
    _users.TryGetValue(id, out var user) ? user : new UserNotFound(id);   // implicit conversions

var message = Find(id) switch
{
    Ok<User>(var user)                    => $"Hello {user.Name}",
    Err<Error>(UserNotFound { Id: var x }) => $"No user {x}",
    Err<Error>(var error)                 => error.Message,
};
```

`Result<T>` is the everyday type, with failures described by `Error` records. `Result<T, TError>` takes any error type: a string, an enum, a struct, or a union of your own errors. The two convert implicitly to each other when `TError` is `Error`.

### Pipelines

```csharp
var dto = await FindAsync(id)
    .Ensure(user => user.IsActive, new UserInactive(id))
    .BindAsync(LoadProfileAsync)
    .Map(ProfileDto.From)
    .TapError(error => logger.LogWarning("{Code}: {Message}", error.Code, error.Message));
```

| Member | Purpose |
| --- | --- |
| `Match` / `Switch` | Handle both cases |
| `Map` / `MapError` | Transform the value / the error |
| `Bind` | Chain an operation that can fail |
| `Ensure` | Turn a value that fails a check into an error |
| `OrElse` | Recover from an error |
| `Tap` / `TapError` | Side effects (logging, metrics) |
| `TryGetOk` / `TryGetErr` | `Try`-pattern access |
| `GetValueOrDefault` / `GetValueOrElse` / `GetValueOrThrow` | Unwrap the value |
| `*Async` and `Task<Result<…>>` extensions | The same, for async code |
| `Select` / `SelectMany` | LINQ query syntax |
| `Result.Try` / `Result.TryAsync` | Turn exceptions into errors at a boundary |
| `Sequence` / `Combine` | Many results → one (fail fast / collect every error) |

Operations that return nothing use `Result<Unit>` and `Result.Ok()`.

## Defining errors

**Open errors.** Derive from `Error`. You get a code, a message, metadata, a cause and value equality:

```csharp
public sealed record InsufficientFunds(decimal Balance, decimal Requested)
    : Error("payments.insufficient_funds", $"Balance {Balance} is less than {Requested}.");

Error error = new InsufficientFunds(10, 25)
    .WithMetadata("accountId", accountId)
    .CausedBy(upstreamError);
```

**Closed errors.** When callers must handle every failure, declare a union of error types and use `Result<T, TError>`. Implement `IError` so generic code can still read a code and message:

```csharp
public sealed record OutOfStock(string Sku) : IError { /* Code, Message */ }
public sealed record PaymentDeclined(decimal Amount) : IError { /* Code, Message */ }

public union OrderError(OutOfStock, PaymentDeclined) : IError
{
    public string Code => this switch { OutOfStock e => e.Code, PaymentDeclined e => e.Code };
    public string Message => this switch { OutOfStock e => e.Message, PaymentDeclined e => e.Message };
}

Result<Order, OrderError> Place(Cart cart) =>
    cart.HasStock ? order : Result.Err<OrderError>(new OutOfStock(cart.Sku));
```

C# applies only one implicit conversion here, so `OutOfStock` does not convert straight to `Result<Order, OrderError>`. Write `Result.Err<OrderError>(…)` or cast to `OrderError` first.

## Performance

`Result<T, TError>` is a struct that stores its value and error inline. Creating, matching and chaining results with `static` lambdas allocates nothing; the test suite checks this. Things that do allocate:

- **Lambdas that capture variables** allocate a closure, as anywhere in .NET. Use `static` lambdas or method groups on hot paths.
- **`Error` records are classes**, so creating one allocates on the failure path. Cache errors you reuse, or use a struct error with `Result<T, TError>` if failures are frequent.
- **Each async step allocates.** Every step in an async pipeline returns a `Task<Result<…>>`, which allocates about 80 B even when it finishes synchronously. When it really waits, it also allocates its async state, just as a hand-written `await` does.
- **The `Value` property** exists only because the C# union pattern requires it, and it boxes. It is hidden from IntelliSense. Use pattern matching, `Match` or `TryGetOk` instead.

### Benchmarks

```
dotnet run -c Release --project benchmarks/Union.Results.Benchmarks -- --filter '*'
```

Use `--filter '*Pipeline*'` to run one group, or `--job short` for a quick, less precise run.

## Things to know

- **`default(Result<…>)` is neither case.** Most members throw `InvalidOperationException` on it, and a `switch` over it throws `SwitchExpressionException`.
- **Pattern matching inside generic code** (`Result<T, TError>` with open `T`) cannot declare variables (C# error CS8780). This is a current C# limitation. Use `Match`, `Switch` or `TryGetOk`/`TryGetErr` there.
- **When `T` and `TError` are the same type** (e.g. `Result<string, string>`), the implicit conversions are ambiguous. Use `Result.Ok(…)` / `Result.Err(…)`.

## License

MIT
