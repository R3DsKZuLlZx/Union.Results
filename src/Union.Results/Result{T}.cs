using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Union.Results;

/// <summary>
/// A union of <see cref="Ok{T}"/> and <see cref="Err{TError}"/> of <see cref="Error"/>: the everyday
/// result type when failures are described by your own <see cref="Error"/> records.
/// </summary>
/// <remarks>
/// <para>
/// This behaves like <see cref="Result{T, TError}"/> with <c>TError</c> fixed to <see cref="Error"/>, and
/// converts implicitly to and from it. Any <see cref="Error"/> subtype converts implicitly to a failed result:
/// <code>
/// Result&lt;User&gt; Find(Guid id) =&gt; users.TryGetValue(id, out var user) ? user : new UserNotFound(id);
/// </code>
/// </para>
/// <para>
/// <c>default(Result&lt;T&gt;)</c> is neither case; most members throw <see cref="InvalidOperationException"/> when called on it.
/// </para>
/// </remarks>
[Union]
[DebuggerDisplay("{ToString(),nq}")]
public readonly struct Result<T> : IUnion, IEquatable<Result<T>>
{
    private readonly Result<T, Error> _inner;

    /// <summary>Creates a successful result.</summary>
    public Result(Ok<T> ok) => _inner = new Result<T, Error>(ok);

    /// <summary>Creates a failed result.</summary>
    public Result(Err<Error> err)
    {
        ArgumentNullException.ThrowIfNull(err.Error);
        _inner = new Result<T, Error>(err);
    }

    private Result(Result<T, Error> inner, bool _) => _inner = inner;

    /// <summary>
    /// The current case boxed as an <see cref="object"/>. This exists for the C# union pattern and
    /// <b>allocates</b>; prefer <see cref="Match{TOut}"/>, <see cref="TryGetOk"/> or pattern matching.
    /// </summary>
    /// <remarks><inheritdoc cref="Result{T, TError}.Value" path="/remarks"/></remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public object Value => _inner.Value;

    /// <summary>
    /// Whether the result was initialized with a case. This exists for the C# union pattern; use
    /// <see cref="IsOk"/> or <see cref="IsErr"/> instead.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool HasValue => _inner.HasValue;

    /// <summary>Whether the result is <see cref="Ok{T}"/>.</summary>
    public bool IsOk => _inner.IsOk;

    /// <summary>Whether the result is <see cref="Err{TError}"/>.</summary>
    public bool IsErr => _inner.IsErr;

    /// <summary>Non-allocating accessor used by the compiler when pattern matching <see cref="Ok{T}"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryGetValue(out Ok<T> value) => _inner.TryGetValue(out value);

    /// <summary>Non-allocating accessor used by the compiler when pattern matching <see cref="Err{TError}"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryGetValue(out Err<Error> value) => _inner.TryGetValue(out value);

    /// <inheritdoc cref="Result{T, TError}.TryGetOk"/>
    public bool TryGetOk([MaybeNullWhen(false)] out T value) => _inner.TryGetOk(out value);

    /// <inheritdoc cref="Result{T, TError}.TryGetErr"/>
    public bool TryGetErr([MaybeNullWhen(false)] out Error error) => _inner.TryGetErr(out error);

    /// <inheritdoc cref="Result{T, TError}.GetValueOrThrow"/>
    public T GetValueOrThrow() => _inner.GetValueOrThrow();

    /// <inheritdoc cref="Result{T, TError}.GetErrorOrThrow"/>
    public Error GetErrorOrThrow() => _inner.GetErrorOrThrow();

    /// <inheritdoc cref="Result{T, TError}.GetValueOrDefault"/>
    public T GetValueOrDefault(T fallback) => _inner.GetValueOrDefault(fallback);

    /// <inheritdoc cref="Result{T, TError}.GetValueOrElse"/>
    public T GetValueOrElse(Func<Error, T> fallback) => _inner.GetValueOrElse(fallback);

    /// <inheritdoc cref="Result{T, TError}.Match{TOut}"/>
    public TOut Match<TOut>(Func<T, TOut> ok, Func<Error, TOut> err) => _inner.Match(ok, err);

    /// <inheritdoc cref="Result{T, TError}.Switch"/>
    public void Switch(Action<T> ok, Action<Error> err) => _inner.Switch(ok, err);

    /// <inheritdoc cref="Result{T, TError}.Map{TOut}"/>
    public Result<TOut> Map<TOut>(Func<T, TOut> map) => new(_inner.Map(map), true);

    /// <summary>Transforms the error of a failed result into another <see cref="Error"/>; values pass through unchanged.</summary>
    /// <remarks>To change to a different error type, convert to <see cref="Result{T, TError}"/> and use its <c>MapError</c>.</remarks>
    public Result<T> MapError(Func<Error, Error> map) => new(_inner.MapError(map), true);

    /// <inheritdoc cref="Result{T, TError}.Bind{TOut}"/>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return _inner.TryGetErr(out var error) ? new Result<TOut>(new Err<Error>(error)) : bind(GetValueOrThrow());
    }

    /// <summary>Chains an operation that may fail and returns nothing onto a successful result; errors short-circuit.</summary>
    public Result Bind(Func<T, Result> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return _inner.TryGetErr(out var error) ? new Result(new Err<Error>(error)) : bind(GetValueOrThrow());
    }

    /// <inheritdoc cref="Result{T, TError}.OrElse"/>
    public Result<T> OrElse(Func<Error, Result<T>> recover)
    {
        ArgumentNullException.ThrowIfNull(recover);
        EnsureInitialized();
        return _inner.TryGetErr(out var error) ? recover(error) : this;
    }

    /// <inheritdoc cref="Result{T, TError}.Tap"/>
    public Result<T> Tap(Action<T> action) => new(_inner.Tap(action), true);

    /// <inheritdoc cref="Result{T, TError}.TapError"/>
    public Result<T> TapError(Action<Error> action) => new(_inner.TapError(action), true);

    /// <inheritdoc cref="Result{T, TError}.Ensure(Func{T, bool}, TError)"/>
    public Result<T> Ensure(Func<T, bool> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(_inner.Ensure(predicate, error), true);
    }

    /// <inheritdoc cref="Result{T, TError}.Ensure(Func{T, bool}, Func{T, TError})"/>
    public Result<T> Ensure(Func<T, bool> predicate, Func<T, Error> error) => new(_inner.Ensure(predicate, error), true);

    /// <inheritdoc cref="Result{T, TError}.MapAsync{TOut}"/>
    public async Task<Result<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        EnsureInitialized();
        return _inner.TryGetErr(out var error)
            ? new Result<TOut>(new Err<Error>(error))
            : new Result<TOut>(new Ok<TOut>(await map(GetValueOrThrow()).ConfigureAwait(false)));
    }

    /// <inheritdoc cref="Result{T, TError}.BindAsync{TOut}"/>
    public Task<Result<TOut>> BindAsync<TOut>(Func<T, Task<Result<TOut>>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return _inner.TryGetErr(out var error)
            ? Task.FromResult(new Result<TOut>(new Err<Error>(error)))
            : bind(GetValueOrThrow());
    }

    /// <summary>Chains an asynchronous operation that may fail and returns nothing; errors short-circuit.</summary>
    public Task<Result> BindAsync(Func<T, Task<Result>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return _inner.TryGetErr(out var error)
            ? Task.FromResult(new Result(new Err<Error>(error)))
            : bind(GetValueOrThrow());
    }

    /// <inheritdoc cref="Result{T, TError}.TapAsync"/>
    public async Task<Result<T>> TapAsync(Func<T, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        EnsureInitialized();
        if (_inner.TryGetOk(out var value))
        {
            await action(value).ConfigureAwait(false);
        }

        return this;
    }

    /// <inheritdoc cref="Result{T, TError}.MatchAsync{TOut}"/>
    public Task<TOut> MatchAsync<TOut>(Func<T, Task<TOut>> ok, Func<Error, Task<TOut>> err) => _inner.MatchAsync(ok, err);

    /// <inheritdoc cref="Result{T, TError}.Select{TOut}"/>
    public Result<TOut> Select<TOut>(Func<T, TOut> selector) => Map(selector);

    /// <inheritdoc cref="Result{T, TError}.SelectMany{TOut}(Func{T, Result{TOut, TError}})"/>
    public Result<TOut> SelectMany<TOut>(Func<T, Result<TOut>> selector) => Bind(selector);

    /// <inheritdoc cref="Result{T, TError}.SelectMany{TMiddle, TOut}(Func{T, Result{TMiddle, TError}}, Func{T, TMiddle, TOut})"/>
    public Result<TOut> SelectMany<TMiddle, TOut>(Func<T, Result<TMiddle>> selector, Func<T, TMiddle, TOut> resultSelector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(resultSelector);
        if (_inner.TryGetErr(out var error))
        {
            return new Result<TOut>(new Err<Error>(error));
        }

        var value = GetValueOrThrow();
        var middle = selector(value);
        return middle.TryGetErr(out var middleError)
            ? new Result<TOut>(new Err<Error>(middleError))
            : new Result<TOut>(new Ok<TOut>(resultSelector(value, middle.GetValueOrThrow())));
    }

    /// <summary>Creates a successful result from a value.</summary>
    public static implicit operator Result<T>(T value) => new(new Ok<T>(value));

    /// <summary>Creates a failed result from an error.</summary>
    public static implicit operator Result<T>(Error error) => new(new Err<Error>(error));

    /// <summary>Converts to the general <see cref="Result{T, TError}"/> form.</summary>
    public static implicit operator Result<T, Error>(Result<T> result) => result._inner;

    /// <summary>Converts from the general <see cref="Result{T, TError}"/> form.</summary>
    public static implicit operator Result<T>(Result<T, Error> result) => new(result, true);

    /// <inheritdoc />
    public bool Equals(Result<T> other) => _inner.Equals(other._inner);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _inner.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => _inner.ToString();

    /// <summary>Compares two results for equality.</summary>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>Compares two results for inequality.</summary>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureInitialized()
    {
        if (!_inner.HasValue)
        {
            ThrowHelper.ThrowDefault();
        }
    }
}
