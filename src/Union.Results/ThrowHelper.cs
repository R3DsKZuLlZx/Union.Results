using System.Diagnostics.CodeAnalysis;
using Union.Errors;

namespace Union.Results;

internal static class ThrowHelper
{
    [DoesNotReturn]
    public static void ThrowDefault() =>
        throw new InvalidOperationException(
            "The result is uninitialized (default). Create results from an Ok or Err value, or via the Result factory methods.");

    [DoesNotReturn]
    public static void ThrowNotOk<TError>(TError error) =>
        throw new InvalidOperationException($"The result is an error: {error}", AsException(error));

    [DoesNotReturn]
    public static void ThrowNotErr<T>(T value) =>
        throw new InvalidOperationException($"The result is not an error; it holds the value: {value}");

    private static Exception? AsException<TError>(TError error) => error switch
    {
        Exception exception => exception,
        ExceptionError exceptionError => exceptionError.Exception,
        _ => null,
    };
}
