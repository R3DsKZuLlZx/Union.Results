using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Union.Results;

/// <summary>
/// A union of <see cref="Ok{T}"/> and <see cref="Err{TError}"/>: either a successful value or an error.
/// </summary>
/// <remarks>
/// <para>
/// The result is a struct that stores its value and error inline, so creating, matching and
/// transforming results does not allocate. Pattern matching over the cases is exhaustive:
/// <code>
/// var text = result switch
/// {
///     Ok&lt;int&gt; ok =&gt; $"Got {ok.Value}",
///     Err&lt;string&gt; err =&gt; $"Failed: {err.Error}",
/// };
/// </code>
/// </para>
/// <para>
/// <c>default(Result&lt;T, TError&gt;)</c> is neither <see cref="Ok{T}"/> nor <see cref="Err{TError}"/>;
/// most members throw <see cref="InvalidOperationException"/> when called on it.
/// </para>
/// </remarks>
[Union]
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct Result<T, TError> : IUnion, IEquatable<Result<T, TError>>
{
    private readonly T _value;
    private readonly TError _error;
    private readonly ResultState _state;

    /// <summary>Creates a successful result.</summary>
    public Result(Ok<T> ok)
    {
        _value = ok.Value;
        _error = default!;
        _state = ResultState.Ok;
    }

    /// <summary>Creates a failed result.</summary>
    public Result(Err<TError> err)
    {
        _value = default!;
        _error = err.Error;
        _state = ResultState.Err;
    }

    /// <summary>
    /// The current case boxed as an <see cref="object"/>. This exists for the C# union pattern and
    /// <b>allocates</b>; prefer <see cref="Match{TOut}"/>, <see cref="TryGetOk"/> or pattern matching.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public object? Value => _state switch
    {
        ResultState.Ok => new Ok<T>(_value),
        ResultState.Err => new Err<TError>(_error),
        _ => null,
    };

    /// <summary>
    /// Whether the result was initialized with a case. This exists for the C# union pattern; use
    /// <see cref="IsOk"/> or <see cref="IsErr"/> instead.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool HasValue => _state != ResultState.Default;

    /// <summary>Whether the result is <see cref="Ok{T}"/>.</summary>
    public bool IsOk => _state == ResultState.Ok;

    /// <summary>Whether the result is <see cref="Err{TError}"/>.</summary>
    public bool IsErr => _state == ResultState.Err;

    /// <summary>Non-allocating accessor used by the compiler when pattern matching <see cref="Ok{T}"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryGetValue(out Ok<T> value)
    {
        value = new Ok<T>(_value);
        return _state == ResultState.Ok;
    }

    /// <summary>Non-allocating accessor used by the compiler when pattern matching <see cref="Err{TError}"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryGetValue(out Err<TError> value)
    {
        value = new Err<TError>(_error);
        return _state == ResultState.Err;
    }

    /// <summary>Gets the value if the result is <see cref="Ok{T}"/>.</summary>
    public bool TryGetOk([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return _state == ResultState.Ok;
    }

    /// <summary>Gets the error if the result is <see cref="Err{TError}"/>.</summary>
    public bool TryGetErr([MaybeNullWhen(false)] out TError error)
    {
        error = _error;
        return _state == ResultState.Err;
    }

    /// <summary>Gets the value, or throws <see cref="InvalidOperationException"/> if the result is an error.</summary>
    public T GetValueOrThrow()
    {
        EnsureInitialized();
        if (_state == ResultState.Err)
        {
            ThrowHelper.ThrowNotOk(_error);
        }

        return _value;
    }

    /// <summary>Gets the error, or throws <see cref="InvalidOperationException"/> if the result is successful.</summary>
    public TError GetErrorOrThrow()
    {
        EnsureInitialized();
        if (_state == ResultState.Ok)
        {
            ThrowHelper.ThrowNotErr(_value);
        }

        return _error;
    }

    /// <summary>Gets the value, or <paramref name="fallback"/> if the result is an error.</summary>
    public T GetValueOrDefault(T fallback)
    {
        EnsureInitialized();
        return _state == ResultState.Ok ? _value : fallback;
    }

    /// <summary>Gets the value, or computes one from the error.</summary>
    public T GetValueOrElse(Func<TError, T> fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        EnsureInitialized();
        return _state == ResultState.Ok ? _value : fallback(_error);
    }

    /// <summary>Returns the output of <paramref name="ok"/> or <paramref name="err"/>, depending on the case.</summary>
    public TOut Match<TOut>(Func<T, TOut> ok, Func<TError, TOut> err)
    {
        ArgumentNullException.ThrowIfNull(ok);
        ArgumentNullException.ThrowIfNull(err);
        EnsureInitialized();
        return _state == ResultState.Ok ? ok(_value) : err(_error);
    }

    /// <summary>Runs <paramref name="ok"/> or <paramref name="err"/>, depending on the case.</summary>
    public void Switch(Action<T> ok, Action<TError> err)
    {
        ArgumentNullException.ThrowIfNull(ok);
        ArgumentNullException.ThrowIfNull(err);
        EnsureInitialized();
        if (_state == ResultState.Ok)
        {
            ok(_value);
        }
        else
        {
            err(_error);
        }
    }

    /// <summary>Transforms the value of a successful result; errors pass through unchanged.</summary>
    public Result<TOut, TError> Map<TOut>(Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        EnsureInitialized();
        return _state == ResultState.Ok
            ? new Result<TOut, TError>(new Ok<TOut>(map(_value)))
            : new Result<TOut, TError>(new Err<TError>(_error));
    }

    /// <summary>Transforms the error of a failed result; values pass through unchanged.</summary>
    public Result<T, TOut> MapError<TOut>(Func<TError, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        EnsureInitialized();
        return _state == ResultState.Ok
            ? new Result<T, TOut>(new Ok<T>(_value))
            : new Result<T, TOut>(new Err<TOut>(map(_error)));
    }

    /// <summary>Chains an operation that may fail onto a successful result; errors short-circuit.</summary>
    public Result<TOut, TError> Bind<TOut>(Func<T, Result<TOut, TError>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        EnsureInitialized();
        return _state == ResultState.Ok
            ? bind(_value)
            : new Result<TOut, TError>(new Err<TError>(_error));
    }

    /// <summary>Replaces a failed result with the output of <paramref name="recover"/>; successful results pass through.</summary>
    public Result<T, TError> OrElse(Func<TError, Result<T, TError>> recover)
    {
        ArgumentNullException.ThrowIfNull(recover);
        EnsureInitialized();
        return _state == ResultState.Err ? recover(_error) : this;
    }

    /// <summary>Runs <paramref name="action"/> on the value of a successful result, then returns the result unchanged.</summary>
    public Result<T, TError> Tap(Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureInitialized();
        if (_state == ResultState.Ok)
        {
            action(_value);
        }

        return this;
    }

    /// <summary>Runs <paramref name="action"/> on the error of a failed result, then returns the result unchanged.</summary>
    public Result<T, TError> TapError(Action<TError> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureInitialized();
        if (_state == ResultState.Err)
        {
            action(_error);
        }

        return this;
    }

    /// <summary>Turns a successful result into <paramref name="error"/> if its value fails <paramref name="predicate"/>.</summary>
    public Result<T, TError> Ensure(Func<T, bool> predicate, TError error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        EnsureInitialized();
        return _state == ResultState.Ok && !predicate(_value)
            ? new Result<T, TError>(new Err<TError>(error))
            : this;
    }

    /// <summary>Turns a successful result into an error built from its value if the value fails <paramref name="predicate"/>.</summary>
    public Result<T, TError> Ensure(Func<T, bool> predicate, Func<T, TError> error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);
        EnsureInitialized();
        return _state == ResultState.Ok && !predicate(_value)
            ? new Result<T, TError>(new Err<TError>(error(_value)))
            : this;
    }

    /// <summary>Transforms the value with an asynchronous function; errors pass through unchanged.</summary>
    public async Task<Result<TOut, TError>> MapAsync<TOut>(Func<T, Task<TOut>> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        EnsureInitialized();
        return _state == ResultState.Ok
            ? new Result<TOut, TError>(new Ok<TOut>(await map(_value).ConfigureAwait(false)))
            : new Result<TOut, TError>(new Err<TError>(_error));
    }

    /// <summary>Chains an asynchronous operation that may fail; errors short-circuit.</summary>
    public Task<Result<TOut, TError>> BindAsync<TOut>(Func<T, Task<Result<TOut, TError>>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        EnsureInitialized();
        return _state == ResultState.Ok
            ? bind(_value)
            : Task.FromResult(new Result<TOut, TError>(new Err<TError>(_error)));
    }

    /// <summary>Runs an asynchronous action on the value of a successful result, then returns the result unchanged.</summary>
    public async Task<Result<T, TError>> TapAsync(Func<T, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureInitialized();
        if (_state == ResultState.Ok)
        {
            await action(_value).ConfigureAwait(false);
        }

        return this;
    }

    /// <summary>Awaits the output of <paramref name="ok"/> or <paramref name="err"/>, depending on the case.</summary>
    public Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> ok, Func<TError, Task<TOut>> err)
    {
        ArgumentNullException.ThrowIfNull(ok);
        ArgumentNullException.ThrowIfNull(err);
        EnsureInitialized();
        return _state == ResultState.Ok ? ok(_value) : err(_error);
    }

    /// <summary>LINQ support: equivalent to <see cref="Map{TOut}"/>.</summary>
    public Result<TOut, TError> Select<TOut>(Func<T, TOut> selector) => Map(selector);

    /// <summary>LINQ support: equivalent to <see cref="Bind{TOut}"/>.</summary>
    public Result<TOut, TError> SelectMany<TOut>(Func<T, Result<TOut, TError>> selector) => Bind(selector);

    /// <summary>LINQ support: enables multiple <c>from</c> clauses in query syntax.</summary>
    public Result<TOut, TError> SelectMany<TMiddle, TOut>(
        Func<T, Result<TMiddle, TError>> selector,
        Func<T, TMiddle, TOut> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        EnsureInitialized();
        if (_state == ResultState.Err)
        {
            return new Result<TOut, TError>(new Err<TError>(_error));
        }

        var middle = selector(_value);
        middle.EnsureInitialized();
        return middle._state == ResultState.Ok
            ? new Result<TOut, TError>(new Ok<TOut>(resultSelector(_value, middle._value)))
            : new Result<TOut, TError>(new Err<TError>(middle._error));
    }

    /// <summary>Creates a successful result from a value.</summary>
    public static implicit operator Result<T, TError>(T value) => new(new Ok<T>(value));

    /// <summary>Creates a failed result from an error.</summary>
    public static implicit operator Result<T, TError>(TError error) => new(new Err<TError>(error));

    /// <inheritdoc />
    public bool Equals(Result<T, TError> other) =>
        _state == other._state && _state switch
        {
            ResultState.Ok => EqualityComparer<T>.Default.Equals(_value, other._value),
            ResultState.Err => EqualityComparer<TError>.Default.Equals(_error, other._error),
            _ => true,
        };

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T, TError> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _state switch
    {
        ResultState.Ok => HashCode.Combine(_state, _value),
        ResultState.Err => HashCode.Combine(_state, _error),
        _ => 0,
    };

    /// <inheritdoc />
    public override string ToString() => _state switch
    {
        ResultState.Ok => $"Ok({_value})",
        ResultState.Err => $"Err({_error})",
        _ => "Result.Default",
    };

    /// <summary>Compares two results for equality.</summary>
    public static bool operator ==(Result<T, TError> left, Result<T, TError> right) => left.Equals(right);

    /// <summary>Compares two results for inequality.</summary>
    public static bool operator !=(Result<T, TError> left, Result<T, TError> right) => !left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureInitialized()
    {
        if (_state == ResultState.Default)
        {
            ThrowHelper.ThrowDefault();
        }
    }
}
