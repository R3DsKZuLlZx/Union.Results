namespace Union.Results;

/// <summary>The failure case of a result, carrying the error.</summary>
/// <param name="Error">The error describing the failure.</param>
public readonly record struct Err<TError>(TError Error);
