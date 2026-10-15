namespace Union.Results;

/// <summary>
/// Extensions on <see cref="Task{TResult}"/> of results, so asynchronous pipelines can be written fluently:
/// <code>
/// var dto = await FindUserAsync(id)
///     .Ensure(user =&gt; user.IsActive, new UserInactive(id))
///     .BindAsync(LoadProfileAsync)
///     .Map(ProfileDto.From);
/// </code>
/// </summary>
public static class ResultTaskExtensions
{
    // Result<T, TError>

    /// <inheritdoc cref="Result{T, TError}.Map{TOut}"/>
    public static async Task<Result<TOut, TError>> Map<T, TError, TOut>(this Task<Result<T, TError>> result, Func<T, TOut> map) =>
        (await result.ConfigureAwait(false)).Map(map);

    /// <inheritdoc cref="Result{T, TError}.MapAsync{TOut}"/>
    public static async Task<Result<TOut, TError>> MapAsync<T, TError, TOut>(this Task<Result<T, TError>> result, Func<T, Task<TOut>> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var awaited = await result.ConfigureAwait(false);
        return awaited.TryGetErr(out var error)
            ? new Result<TOut, TError>(new Err<TError>(error))
            : new Result<TOut, TError>(new Ok<TOut>(await map(awaited.GetValueOrThrow()).ConfigureAwait(false)));
    }

    /// <inheritdoc cref="Result{T, TError}.MapError{TOut}"/>
    public static async Task<Result<T, TOut>> MapError<T, TError, TOut>(this Task<Result<T, TError>> result, Func<TError, TOut> map) =>
        (await result.ConfigureAwait(false)).MapError(map);

    /// <inheritdoc cref="Result{T, TError}.Bind{TOut}"/>
    public static async Task<Result<TOut, TError>> Bind<T, TError, TOut>(this Task<Result<T, TError>> result, Func<T, Result<TOut, TError>> bind) =>
        (await result.ConfigureAwait(false)).Bind(bind);

    /// <inheritdoc cref="Result{T, TError}.BindAsync{TOut}"/>
    public static async Task<Result<TOut, TError>> BindAsync<T, TError, TOut>(this Task<Result<T, TError>> result, Func<T, Task<Result<TOut, TError>>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        var awaited = await result.ConfigureAwait(false);
        return awaited.TryGetErr(out var error)
            ? new Result<TOut, TError>(new Err<TError>(error))
            : await bind(awaited.GetValueOrThrow()).ConfigureAwait(false);
    }

    /// <inheritdoc cref="Result{T, TError}.Tap"/>
    public static async Task<Result<T, TError>> Tap<T, TError>(this Task<Result<T, TError>> result, Action<T> action) =>
        (await result.ConfigureAwait(false)).Tap(action);

    /// <inheritdoc cref="Result{T, TError}.TapAsync"/>
    public static async Task<Result<T, TError>> TapAsync<T, TError>(this Task<Result<T, TError>> result, Func<T, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var awaited = await result.ConfigureAwait(false);
        if (!awaited.HasValue)
        {
            ThrowHelper.ThrowDefault();
        }

        if (awaited.TryGetOk(out var value))
        {
            await action(value).ConfigureAwait(false);
        }

        return awaited;
    }

    /// <inheritdoc cref="Result{T, TError}.TapError"/>
    public static async Task<Result<T, TError>> TapError<T, TError>(this Task<Result<T, TError>> result, Action<TError> action) =>
        (await result.ConfigureAwait(false)).TapError(action);

    /// <inheritdoc cref="Result{T, TError}.Ensure(Func{T, bool}, TError)"/>
    public static async Task<Result<T, TError>> Ensure<T, TError>(this Task<Result<T, TError>> result, Func<T, bool> predicate, TError error) =>
        (await result.ConfigureAwait(false)).Ensure(predicate, error);

    /// <inheritdoc cref="Result{T, TError}.Match{TOut}"/>
    public static async Task<TOut> Match<T, TError, TOut>(this Task<Result<T, TError>> result, Func<T, TOut> ok, Func<TError, TOut> err) =>
        (await result.ConfigureAwait(false)).Match(ok, err);

    /// <inheritdoc cref="Result{T, TError}.MatchAsync{TOut}"/>
    public static async Task<TOut> MatchAsync<T, TError, TOut>(this Task<Result<T, TError>> result, Func<T, Task<TOut>> ok, Func<TError, Task<TOut>> err) =>
        await (await result.ConfigureAwait(false)).MatchAsync(ok, err).ConfigureAwait(false);

    // Result<T>

    /// <inheritdoc cref="Result{T}.Map{TOut}"/>
    public static async Task<Result<TOut>> Map<T, TOut>(this Task<Result<T>> result, Func<T, TOut> map) =>
        (await result.ConfigureAwait(false)).Map(map);

    /// <inheritdoc cref="Result{T}.MapAsync{TOut}"/>
    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Task<Result<T>> result, Func<T, Task<TOut>> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var awaited = await result.ConfigureAwait(false);
        return awaited.TryGetErr(out var error)
            ? new Result<TOut>(new Err<Error>(error))
            : new Result<TOut>(new Ok<TOut>(await map(awaited.GetValueOrThrow()).ConfigureAwait(false)));
    }

    /// <inheritdoc cref="Result{T}.MapError"/>
    public static async Task<Result<T>> MapError<T>(this Task<Result<T>> result, Func<Error, Error> map) =>
        (await result.ConfigureAwait(false)).MapError(map);

    /// <inheritdoc cref="Result{T}.Bind{TOut}"/>
    public static async Task<Result<TOut>> Bind<T, TOut>(this Task<Result<T>> result, Func<T, Result<TOut>> bind) =>
        (await result.ConfigureAwait(false)).Bind(bind);

    /// <inheritdoc cref="Result{T}.BindAsync{TOut}"/>
    public static async Task<Result<TOut>> BindAsync<T, TOut>(this Task<Result<T>> result, Func<T, Task<Result<TOut>>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        var awaited = await result.ConfigureAwait(false);
        return awaited.TryGetErr(out var error)
            ? new Result<TOut>(new Err<Error>(error))
            : await bind(awaited.GetValueOrThrow()).ConfigureAwait(false);
    }

    /// <inheritdoc cref="Result{T}.Tap"/>
    public static async Task<Result<T>> Tap<T>(this Task<Result<T>> result, Action<T> action) =>
        (await result.ConfigureAwait(false)).Tap(action);

    /// <inheritdoc cref="Result{T}.TapAsync"/>
    public static async Task<Result<T>> TapAsync<T>(this Task<Result<T>> result, Func<T, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var awaited = await result.ConfigureAwait(false);
        if (!awaited.HasValue)
        {
            ThrowHelper.ThrowDefault();
        }

        if (awaited.TryGetOk(out var value))
        {
            await action(value).ConfigureAwait(false);
        }

        return awaited;
    }

    /// <inheritdoc cref="Result{T}.TapError"/>
    public static async Task<Result<T>> TapError<T>(this Task<Result<T>> result, Action<Error> action) =>
        (await result.ConfigureAwait(false)).TapError(action);

    /// <inheritdoc cref="Result{T}.Ensure(Func{T, bool}, Error)"/>
    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> result, Func<T, bool> predicate, Error error) =>
        (await result.ConfigureAwait(false)).Ensure(predicate, error);

    /// <inheritdoc cref="Result{T}.Match{TOut}"/>
    public static async Task<TOut> Match<T, TOut>(this Task<Result<T>> result, Func<T, TOut> ok, Func<Error, TOut> err) =>
        (await result.ConfigureAwait(false)).Match(ok, err);

    /// <inheritdoc cref="Result{T}.MatchAsync{TOut}"/>
    public static async Task<TOut> MatchAsync<T, TOut>(this Task<Result<T>> result, Func<T, Task<TOut>> ok, Func<Error, Task<TOut>> err) =>
        await (await result.ConfigureAwait(false)).MatchAsync(ok, err).ConfigureAwait(false);

    /// <inheritdoc cref="Result{T}.Bind(Func{T, Result})"/>
    public static async Task<Result> Bind<T>(this Task<Result<T>> result, Func<T, Result> bind) =>
        (await result.ConfigureAwait(false)).Bind(bind);

    /// <inheritdoc cref="Result{T}.BindAsync(Func{T, Task{Result}})"/>
    public static async Task<Result> BindAsync<T>(this Task<Result<T>> result, Func<T, Task<Result>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        var awaited = await result.ConfigureAwait(false);
        return awaited.TryGetErr(out var error)
            ? new Result(new Err<Error>(error))
            : await bind(awaited.GetValueOrThrow()).ConfigureAwait(false);
    }

    // Result

    /// <inheritdoc cref="Result.Map{TOut}"/>
    public static async Task<Result<TOut>> Map<TOut>(this Task<Result> result, Func<TOut> map) =>
        (await result.ConfigureAwait(false)).Map(map);

    /// <inheritdoc cref="Result.MapAsync{TOut}"/>
    public static async Task<Result<TOut>> MapAsync<TOut>(this Task<Result> result, Func<Task<TOut>> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var awaited = await result.ConfigureAwait(false);
        awaited.ThrowIfDefault();
        return awaited.TryGetErr(out var error)
            ? new Result<TOut>(new Err<Error>(error))
            : new Result<TOut>(new Ok<TOut>(await map().ConfigureAwait(false)));
    }

    /// <inheritdoc cref="Result.MapError"/>
    public static async Task<Result> MapError(this Task<Result> result, Func<Error, Error> map) =>
        (await result.ConfigureAwait(false)).MapError(map);

    /// <inheritdoc cref="Result.Bind(Func{Result})"/>
    public static async Task<Result> Bind(this Task<Result> result, Func<Result> bind) =>
        (await result.ConfigureAwait(false)).Bind(bind);

    /// <inheritdoc cref="Result.Bind{TOut}(Func{Result{TOut}})"/>
    public static async Task<Result<TOut>> Bind<TOut>(this Task<Result> result, Func<Result<TOut>> bind) =>
        (await result.ConfigureAwait(false)).Bind(bind);

    /// <inheritdoc cref="Result.BindAsync(Func{Task{Result}})"/>
    public static async Task<Result> BindAsync(this Task<Result> result, Func<Task<Result>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        var awaited = await result.ConfigureAwait(false);
        awaited.ThrowIfDefault();
        return awaited.IsErr ? awaited : await bind().ConfigureAwait(false);
    }

    /// <inheritdoc cref="Result.BindAsync{TOut}(Func{Task{Result{TOut}}})"/>
    public static async Task<Result<TOut>> BindAsync<TOut>(this Task<Result> result, Func<Task<Result<TOut>>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        var awaited = await result.ConfigureAwait(false);
        awaited.ThrowIfDefault();
        return awaited.TryGetErr(out var error)
            ? new Result<TOut>(new Err<Error>(error))
            : await bind().ConfigureAwait(false);
    }

    /// <inheritdoc cref="Result.Tap"/>
    public static async Task<Result> Tap(this Task<Result> result, Action action) =>
        (await result.ConfigureAwait(false)).Tap(action);

    /// <inheritdoc cref="Result.TapAsync"/>
    public static async Task<Result> TapAsync(this Task<Result> result, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var awaited = await result.ConfigureAwait(false);
        awaited.ThrowIfDefault();
        if (awaited.IsOk)
        {
            await action().ConfigureAwait(false);
        }

        return awaited;
    }

    /// <inheritdoc cref="Result.TapError"/>
    public static async Task<Result> TapError(this Task<Result> result, Action<Error> action) =>
        (await result.ConfigureAwait(false)).TapError(action);

    /// <inheritdoc cref="Result.Ensure(Func{bool}, Error)"/>
    public static async Task<Result> Ensure(this Task<Result> result, Func<bool> predicate, Error error) =>
        (await result.ConfigureAwait(false)).Ensure(predicate, error);

    /// <inheritdoc cref="Result.Match{TOut}"/>
    public static async Task<TOut> Match<TOut>(this Task<Result> result, Func<TOut> ok, Func<Error, TOut> err) =>
        (await result.ConfigureAwait(false)).Match(ok, err);

    /// <inheritdoc cref="Result.MatchAsync{TOut}"/>
    public static async Task<TOut> MatchAsync<TOut>(this Task<Result> result, Func<Task<TOut>> ok, Func<Error, Task<TOut>> err) =>
        await (await result.ConfigureAwait(false)).MatchAsync(ok, err).ConfigureAwait(false);

    private static void ThrowIfDefault(this Result result)
    {
        if (!result.HasValue)
        {
            ThrowHelper.ThrowDefault();
        }
    }
}
