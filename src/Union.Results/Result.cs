using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Union.Results;

/// <summary>
/// A union of <see cref="Ok"/> and <see cref="Err{TError}"/> of <see cref="Error"/>: the result of an
/// operation that either succeeds with nothing to return or fails.
/// </summary>
/// <remarks>
/// <para>
/// Also hosts the factory methods for every result type, e.g. <see cref="Ok{T}(T)"/>, <see cref="Err{TError}"/>
/// and <see cref="Try{T}(Func{T})"/>.
/// <code>
/// Result Save(Order order) =&gt; order.IsValid ? Result.Ok() : new InvalidOrder(order.Id);
///
/// var message = Save(order) switch
/// {
///     Ok =&gt; "Saved",
///     Err&lt;Error&gt;(var error) =&gt; error.Message,
/// };
/// </code>
/// </para>
/// <para>
/// <c>default(Result)</c> is neither case; most members throw <see cref="InvalidOperationException"/> when called on it.
/// </para>
/// </remarks>
[Union]
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct Result : IUnion, IEquatable<Result>
{
    private readonly Error? _error;
    private readonly ResultState _state;

    /// <summary>Creates a successful result.</summary>
    public Result(Ok ok)
    {
        _error = null;
        _state = ResultState.Ok;
    }

    /// <summary>Creates a failed result.</summary>
    public Result(Err<Error> err)
    {
        ArgumentNullException.ThrowIfNull(err.Error);
        _error = err.Error;
        _state = ResultState.Err;
    }

    /// <summary>
    /// The current case boxed as an <see cref="object"/>. This exists for the C# union pattern and
    /// <b>allocates</b>; prefer <see cref="Match{TOut}"/>, <see cref="IsOk"/> or pattern matching.
    /// </summary>
    /// <remarks><inheritdoc cref="Result{T, TError}.Value" path="/remarks"/></remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public object Value => _state switch
    {
        ResultState.Ok => new Ok(),
        ResultState.Err => new Err<Error>(_error!),
        _ => null!,
    };

    /// <summary>
    /// Whether the result was initialized with a case. This exists for the C# union pattern; use
    /// <see cref="IsOk"/> or <see cref="IsErr"/> instead.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool HasValue => _state != ResultState.Default;

    /// <summary>Whether the result is <see cref="Ok"/>.</summary>
    public bool IsOk => _state == ResultState.Ok;

    /// <summary>Whether the result is <see cref="Err{TError}"/>.</summary>
    public bool IsErr => _state == ResultState.Err;

    /// <summary>Non-allocating accessor used by the compiler when pattern matching <see cref="Ok"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryGetValue(out Ok value)
    {
        value = default;
        return _state == ResultState.Ok;
    }

    /// <summary>Non-allocating accessor used by the compiler when pattern matching <see cref="Err{TError}"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryGetValue(out Err<Error> value)
    {
        value = new Err<Error>(_error!);
        return _state == ResultState.Err;
    }

    /// <summary>Gets the error if the result is <see cref="Err{TError}"/>.</summary>
    public bool TryGetErr([NotNullWhen(true)] out Error? error)
    {
        error = _error;
        return _state == ResultState.Err;
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> if the result is an error.</summary>
    public void ThrowIfErr()
    {
        EnsureInitialized();
        if (_state == ResultState.Err)
        {
            ThrowHelper.ThrowNotOk(_error);
        }
    }

    /// <summary>Gets the error, or throws <see cref="InvalidOperationException"/> if the result is successful.</summary>
    public Error GetErrorOrThrow()
    {
        EnsureInitialized();
        if (_state == ResultState.Ok)
        {
            ThrowHelper.ThrowNotErr("Ok");
        }

        return _error!;
    }

    /// <summary>Returns the output of <paramref name="ok"/> or <paramref name="err"/>, depending on the case.</summary>
    public TOut Match<TOut>(Func<TOut> ok, Func<Error, TOut> err)
    {
        ArgumentNullException.ThrowIfNull(ok);
        ArgumentNullException.ThrowIfNull(err);
        EnsureInitialized();
        return _state == ResultState.Ok ? ok() : err(_error!);
    }

    /// <summary>Runs <paramref name="ok"/> or <paramref name="err"/>, depending on the case.</summary>
    public void Switch(Action ok, Action<Error> err)
    {
        ArgumentNullException.ThrowIfNull(ok);
        ArgumentNullException.ThrowIfNull(err);
        EnsureInitialized();
        if (_state == ResultState.Ok)
        {
            ok();
        }
        else
        {
            err(_error!);
        }
    }

    /// <summary>Produces a value after a successful result; errors pass through unchanged.</summary>
    public Result<TOut> Map<TOut>(Func<TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        EnsureInitialized();
        return _state == ResultState.Ok
            ? new Result<TOut>(new Ok<TOut>(map()))
            : new Result<TOut>(new Err<Error>(_error!));
    }

    /// <summary>Transforms the error of a failed result; successful results pass through unchanged.</summary>
    public Result MapError(Func<Error, Error> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        EnsureInitialized();
        return _state == ResultState.Err ? new Result(new Err<Error>(map(_error!))) : this;
    }

    /// <summary>Chains another operation that may fail onto a successful result; errors short-circuit.</summary>
    public Result Bind(Func<Result> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        EnsureInitialized();
        return _state == ResultState.Ok ? bind() : this;
    }

    /// <summary>Chains an operation that may fail and produces a value; errors short-circuit.</summary>
    public Result<TOut> Bind<TOut>(Func<Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        EnsureInitialized();
        return _state == ResultState.Ok ? bind() : new Result<TOut>(new Err<Error>(_error!));
    }

    /// <summary>Replaces a failed result with the output of <paramref name="recover"/>; successful results pass through.</summary>
    public Result OrElse(Func<Error, Result> recover)
    {
        ArgumentNullException.ThrowIfNull(recover);
        EnsureInitialized();
        return _state == ResultState.Err ? recover(_error!) : this;
    }

    /// <summary>Runs <paramref name="action"/> if the result is successful, then returns the result unchanged.</summary>
    public Result Tap(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureInitialized();
        if (_state == ResultState.Ok)
        {
            action();
        }

        return this;
    }

    /// <summary>Runs <paramref name="action"/> on the error of a failed result, then returns the result unchanged.</summary>
    public Result TapError(Action<Error> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureInitialized();
        if (_state == ResultState.Err)
        {
            action(_error!);
        }

        return this;
    }

    /// <summary>Turns a successful result into <paramref name="error"/> if <paramref name="predicate"/> is false.</summary>
    public Result Ensure(Func<bool> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        EnsureInitialized();
        return _state == ResultState.Ok && !predicate() ? new Result(new Err<Error>(error)) : this;
    }

    /// <summary>Turns a successful result into the output of <paramref name="error"/> if <paramref name="predicate"/> is false.</summary>
    public Result Ensure(Func<bool> predicate, Func<Error> error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        EnsureInitialized();
        return _state == ResultState.Ok && !predicate() ? new Result(new Err<Error>(error())) : this;
    }

    /// <summary>Produces a value asynchronously after a successful result; errors pass through unchanged.</summary>
    public async Task<Result<TOut>> MapAsync<TOut>(Func<Task<TOut>> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        EnsureInitialized();
        return _state == ResultState.Ok
            ? new Result<TOut>(new Ok<TOut>(await map().ConfigureAwait(false)))
            : new Result<TOut>(new Err<Error>(_error!));
    }

    /// <summary>Chains another asynchronous operation that may fail; errors short-circuit.</summary>
    public Task<Result> BindAsync(Func<Task<Result>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        EnsureInitialized();
        return _state == ResultState.Ok ? bind() : Task.FromResult(this);
    }

    /// <summary>Chains an asynchronous operation that may fail and produces a value; errors short-circuit.</summary>
    public Task<Result<TOut>> BindAsync<TOut>(Func<Task<Result<TOut>>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        EnsureInitialized();
        return _state == ResultState.Ok ? bind() : Task.FromResult(new Result<TOut>(new Err<Error>(_error!)));
    }

    /// <summary>Runs an asynchronous action if the result is successful, then returns the result unchanged.</summary>
    public async Task<Result> TapAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureInitialized();
        if (_state == ResultState.Ok)
        {
            await action().ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>Awaits the output of <paramref name="ok"/> or <paramref name="err"/>, depending on the case.</summary>
    public Task<TOut> MatchAsync<TOut>(Func<Task<TOut>> ok, Func<Error, Task<TOut>> err)
    {
        ArgumentNullException.ThrowIfNull(ok);
        ArgumentNullException.ThrowIfNull(err);
        EnsureInitialized();
        return _state == ResultState.Ok ? ok() : err(_error!);
    }

    /// <summary>Creates a failed result from an error.</summary>
    public static implicit operator Result(Error error) => new(new Err<Error>(error));

    /// <inheritdoc />
    public bool Equals(Result other) =>
        _state == other._state && (_state != ResultState.Err || _error!.Equals(other._error));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_state, _error);

    /// <inheritdoc />
    public override string ToString() => _state switch
    {
        ResultState.Ok => "Ok",
        ResultState.Err => $"Err({_error})",
        _ => "Result.Default",
    };

    /// <summary>Compares two results for equality.</summary>
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>Compares two results for inequality.</summary>
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    // Factories

    /// <summary>Creates the success case. It converts implicitly to any result with a matching value type.</summary>
    public static Ok<T> Ok<T>(T value) => new(value);

    /// <summary>
    /// Creates the success case with no value. It converts implicitly to <see cref="Result"/> and to
    /// <see cref="Result{T, TError}"/> of <see cref="Union.Results.Ok"/>, e.g. <c>Result&lt;Ok, OrderError&gt;</c>.
    /// </summary>
    public static Ok Ok() => default;

    /// <summary>
    /// Creates the failure case. It converts implicitly to any result with a matching error type.
    /// Specify <typeparamref name="TError"/> to convert a case into your own error union, e.g.
    /// <c>Result.Err&lt;OrderError&gt;(new OutOfStock(sku))</c>.
    /// </summary>
    public static Err<TError> Err<TError>(TError error) => new(error);

    /// <summary>Runs <paramref name="action"/>, capturing any exception as an <see cref="ExceptionError"/>.</summary>
    /// <remarks><see cref="OperationCanceledException"/> is not captured, so cancellation still propagates.</remarks>
    public static Result Try(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            action();
            return Ok();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ExceptionError(exception);
        }
    }

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
    public static async Task<Result> TryAsync(Func<Task> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        try
        {
            await func().ConfigureAwait(false);
            return Ok();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ExceptionError(exception);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureInitialized()
    {
        if (_state == ResultState.Default)
        {
            ThrowHelper.ThrowDefault();
        }
    }
}
