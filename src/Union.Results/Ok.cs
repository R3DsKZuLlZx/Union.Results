namespace Union.Results;

/// <summary>The success case of a result, carrying the value.</summary>
/// <param name="Value">The successful value.</param>
public readonly record struct Ok<T>(T Value);

/// <summary>
/// The success case of a result with no value, used by <see cref="Result"/> and as the value type of
/// <see cref="Result{T, TError}"/> for operations that return nothing, e.g. <c>Result&lt;Ok, OrderError&gt;</c>.
/// </summary>
public readonly record struct Ok
{
    /// <inheritdoc />
    public override string ToString() => "Ok";
}
