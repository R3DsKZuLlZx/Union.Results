namespace Union.Errors;

/// <summary>
/// The minimal contract shared by all errors. Implement this on your own error types
/// (records, structs, or C# unions of errors) so generic code such as logging or HTTP
/// mapping can describe them without knowing the concrete type.
/// </summary>
public interface IError
{
    /// <summary>A stable, machine-readable identifier, e.g. <c>"orders.not_found"</c>.</summary>
    string Code { get; }

    /// <summary>A human-readable description of what went wrong.</summary>
    string Message { get; }
}
