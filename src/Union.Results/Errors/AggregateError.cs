using System.Text;

namespace Union.Results;

/// <summary>An error made up of several errors, e.g. the failures from validating many fields.</summary>
public sealed record AggregateError : Error
{
    /// <summary>The <see cref="Error.Code"/> used by every <see cref="AggregateError"/>.</summary>
    public const string ErrorCode = "aggregate";

    /// <summary>Creates an aggregate of <paramref name="errors"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty.</exception>
    public AggregateError(IEnumerable<Error> errors)
        : this(Materialize(errors))
    {
    }

    private AggregateError(Error[] errors)
        : base(ErrorCode, $"{errors.Length} error(s) occurred.")
    {
        Errors = errors;
    }

    /// <summary>The individual errors, in the order they occurred.</summary>
    public IReadOnlyList<Error> Errors { get; }

    /// <inheritdoc />
    public bool Equals(AggregateError? other) =>
        base.Equals(other) && Errors.SequenceEqual(other.Errors);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Errors.Count);

    /// <inheritdoc />
    protected override bool PrintMembers(StringBuilder builder)
    {
        base.PrintMembers(builder);
        builder.Append(", Errors = [ ").AppendJoin(", ", Errors).Append(" ]");
        return true;
    }

    private static Error[] Materialize(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var array = errors.ToArray();
        if (array.Length == 0)
        {
            throw new ArgumentException("An AggregateError requires at least one error.", nameof(errors));
        }

        return array;
    }
}
