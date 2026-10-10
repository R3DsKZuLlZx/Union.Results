namespace Union.Errors;

/// <summary>An error that wraps an <see cref="System.Exception"/> caught at a boundary.</summary>
/// <param name="Exception">The exception that was caught.</param>
public sealed record ExceptionError(Exception Exception) : Error(ErrorCode, Exception.Message)
{
    /// <summary>The <see cref="Error.Code"/> used by every <see cref="ExceptionError"/>.</summary>
    public const string ErrorCode = "exception";
}
