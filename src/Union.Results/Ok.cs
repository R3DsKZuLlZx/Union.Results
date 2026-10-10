namespace Union.Results;

/// <summary>The success case of a result, carrying the value.</summary>
/// <param name="Value">The successful value.</param>
public readonly record struct Ok<T>(T Value);
