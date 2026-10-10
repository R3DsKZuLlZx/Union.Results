namespace Union.Results;

/// <summary>
/// A type with a single value, used as the success value of operations that return nothing,
/// e.g. <c>Result&lt;Unit&gt;</c>.
/// </summary>
public readonly record struct Unit
{
    /// <summary>The single <see cref="Unit"/> value.</summary>
    public static Unit Value => default;

    /// <inheritdoc />
    public override string ToString() => "()";
}
