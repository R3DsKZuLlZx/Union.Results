using Union.Errors;

namespace Union.Results;

/// <summary>Factory and helper methods for <see cref="Result{T}"/> and <see cref="Result{T, TError}"/>.</summary>
public static class Result
{
    /// <summary>Creates the success case. It converts implicitly to any result with a matching value type.</summary>
    public static Ok<T> Ok<T>(T value) => new(value);

    /// <summary>Creates the success case for operations that return nothing, e.g. <c>Result&lt;Unit&gt;</c>.</summary>
    public static Ok<Unit> Ok() => new(Unit.Value);

    /// <summary>
    /// Creates the failure case. It converts implicitly to any result with a matching error type.
    /// Specify <typeparamref name="TError"/> to convert a case into your own error union, e.g.
    /// <c>Result.Err&lt;OrderError&gt;(new OutOfStock(sku))</c>.
    /// </summary>
    public static Err<TError> Err<TError>(TError error) => new(error);

    /// <summary>Runs <paramref name="func"/>, capturing any exception as an <see cref="ExceptionError"/>.</summary>
    /// <remarks><see cref="OperationCanceledException"/> is not captured, so cancellation still propagates.</remarks>
    public static Result<T> Try<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        try
        {
            return func();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ExceptionError(exception);
        }
    }

    /// <summary>Runs <paramref name="func"/>, mapping any exception to an error with <paramref name="onException"/>.</summary>
    /// <remarks><see cref="OperationCanceledException"/> is not captured, so cancellation still propagates.</remarks>
    public static Result<T, TError> Try<T, TError>(Func<T> func, Func<Exception, TError> onException)
    {
        ArgumentNullException.ThrowIfNull(func);
        ArgumentNullException.ThrowIfNull(onException);
        try
        {
            return new Result<T, TError>(new Ok<T>(func()));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new Result<T, TError>(new Err<TError>(onException(exception)));
        }
    }

    /// <summary>Awaits <paramref name="func"/>, capturing any exception as an <see cref="ExceptionError"/>.</summary>
    /// <remarks><see cref="OperationCanceledException"/> is not captured, so cancellation still propagates.</remarks>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        try
        {
            return await func().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ExceptionError(exception);
        }
    }

    /// <summary>Awaits <paramref name="func"/>, mapping any exception to an error with <paramref name="onException"/>.</summary>
    /// <remarks><see cref="OperationCanceledException"/> is not captured, so cancellation still propagates.</remarks>
    public static async Task<Result<T, TError>> TryAsync<T, TError>(Func<Task<T>> func, Func<Exception, TError> onException)
    {
        ArgumentNullException.ThrowIfNull(func);
        ArgumentNullException.ThrowIfNull(onException);
        try
        {
            return new Result<T, TError>(new Ok<T>(await func().ConfigureAwait(false)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new Result<T, TError>(new Err<TError>(onException(exception)));
        }
    }

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

        return errors switch
        {
            null => new Result<IReadOnlyList<T>>(new Ok<IReadOnlyList<T>>(values)),
            [var single] => new Result<IReadOnlyList<T>>(new Err<Error>(single)),
            _ => new Result<IReadOnlyList<T>>(new Err<Error>(new AggregateError(errors))),
        };
    }
}
