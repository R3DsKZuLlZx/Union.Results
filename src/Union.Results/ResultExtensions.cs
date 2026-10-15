namespace Union.Results;

/// <summary>Helpers for working with collections of results.</summary>
public static class ResultExtensions
{
    /// <summary>
    /// Collects the values of all results, or returns the first error (fail fast).
    /// </summary>
    public static Result<IReadOnlyList<T>, TError> Sequence<T, TError>(this IEnumerable<Result<T, TError>> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var values = new List<T>();
        foreach (var result in results)
        {
            if (result.TryGetErr(out var error))
            {
                return new Result<IReadOnlyList<T>, TError>(new Err<TError>(error));
            }

            values.Add(result.GetValueOrThrow());
        }

        return new Result<IReadOnlyList<T>, TError>(new Ok<IReadOnlyList<T>>(values));
    }

    /// <summary>
    /// Collects the values of all results, or returns the first error (fail fast).
    /// </summary>
    public static Result<IReadOnlyList<T>> Sequence<T>(this IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.Select(static r => (Result<T, Error>)r).Sequence();
    }

    /// <summary>
    /// Succeeds if every result succeeded, or returns the first error (fail fast).
    /// </summary>
    public static Result Sequence(this IEnumerable<Result> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        foreach (var result in results)
        {
            if (result.TryGetErr(out var error))
            {
                return error;
            }

            result.ThrowIfErr();
        }

        return new Ok();
    }

    /// <summary>
    /// Collects the values of all results, or every error that occurred (validation style).
    /// A single error is returned as-is; several are wrapped in an <see cref="AggregateError"/>.
    /// </summary>
    public static Result<IReadOnlyList<T>> Combine<T>(this IEnumerable<Result<T>> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var values = new List<T>();
        List<Error>? errors = null;
        foreach (var result in results)
        {
            if (result.TryGetErr(out var error))
            {
                (errors ??= []).Add(error);
            }
            else
            {
                values.Add(result.GetValueOrThrow());
            }
        }

        return errors is null
            ? new Result<IReadOnlyList<T>>(new Ok<IReadOnlyList<T>>(values))
            : new Result<IReadOnlyList<T>>(new Err<Error>(Collapse(errors)));
    }

    /// <summary>
    /// Succeeds if every result succeeded, or returns every error that occurred (validation style).
    /// A single error is returned as-is; several are wrapped in an <see cref="AggregateError"/>.
    /// </summary>
    public static Result Combine(this IEnumerable<Result> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        List<Error>? errors = null;
        foreach (var result in results)
        {
            if (result.TryGetErr(out var error))
            {
                (errors ??= []).Add(error);
            }
            else
            {
                result.ThrowIfErr();
            }
        }

        return errors is null ? new Ok() : Collapse(errors);
    }

    private static Error Collapse(List<Error> errors) =>
        errors is [var single] ? single : new AggregateError(errors);
}
