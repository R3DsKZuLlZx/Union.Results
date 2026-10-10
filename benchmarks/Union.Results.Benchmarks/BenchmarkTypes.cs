namespace Union.Results.Benchmarks;

public sealed record NotFound(int Id) : Error("benchmarks.not_found", "Not found.");

public sealed class NotFoundException(int id) : Exception($"{id} was not found.");

public enum Outcome
{
    Ok,
    Err,
}

public enum Completion
{
    Sync,
    Async,
}
