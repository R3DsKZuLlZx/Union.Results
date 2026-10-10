using System.Collections.Frozen;
using System.Text;

namespace Union.Errors;

/// <summary>
/// A base record for defining your own errors. Derive from it to get a code, a message,
/// optional metadata and an optional cause, along with value equality.
/// </summary>
/// <example>
/// <code>
/// public sealed record UserNotFound(Guid UserId)
///     : Error("users.not_found", $"User '{UserId}' was not found.");
/// </code>
/// </example>
/// <param name="Code">A stable, machine-readable identifier, e.g. <c>"orders.not_found"</c>.</param>
/// <param name="Message">A human-readable description of what went wrong.</param>
public abstract record Error(string Code, string Message) : IError
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyMetadata =
        FrozenDictionary<string, object?>.Empty;

    /// <summary>Arbitrary structured data describing the error.</summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = EmptyMetadata;

    /// <summary>The underlying error that caused this one, if any.</summary>
    public Error? Cause { get; init; }

    /// <summary>Returns a copy of this error with <paramref name="key"/> set to <paramref name="value"/> in <see cref="Metadata"/>.</summary>
    public Error WithMetadata(string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        var metadata = new Dictionary<string, object?>(Metadata) { [key] = value };
        return this with { Metadata = metadata };
    }

    /// <summary>Returns a copy of this error with <see cref="Cause"/> set to <paramref name="cause"/>.</summary>
    public Error CausedBy(Error cause)
    {
        ArgumentNullException.ThrowIfNull(cause);
        return this with { Cause = cause };
    }

    /// <inheritdoc />
    public virtual bool Equals(Error? other) =>
        other is not null
        && EqualityContract == other.EqualityContract
        && Code == other.Code
        && Message == other.Message
        && Equals(Cause, other.Cause)
        && MetadataEquals(Metadata, other.Metadata);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(EqualityContract, Code, Message, Cause);

    /// <summary>Formats the error's members for <see cref="ToString"/>, omitting empty metadata and a missing cause.</summary>
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Code = ").Append(Code).Append(", Message = ").Append(Message);

        if (Metadata.Count > 0)
        {
            builder.Append(", Metadata = { ");
            builder.AppendJoin(", ", Metadata.Select(static kv => $"{kv.Key} = {kv.Value}"));
            builder.Append(" }");
        }

        if (Cause is not null)
        {
            builder.Append(", Cause = ").Append(Cause);
        }

        return true;
    }

    private static bool MetadataEquals(IReadOnlyDictionary<string, object?> left, IReadOnlyDictionary<string, object?> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var otherValue) || !Equals(value, otherValue))
            {
                return false;
            }
        }

        return true;
    }
}
