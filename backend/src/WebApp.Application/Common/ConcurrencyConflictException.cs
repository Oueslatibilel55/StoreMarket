namespace WebApp.Application.Common;

/// <summary>
/// Someone else changed the same record between our read and our save (optimistic concurrency). Thrown by the
/// unit of work so services can answer "it changed, reload" without knowing the persistence library.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException() { }
    public ConcurrencyConflictException(string message) : base(message) { }
    public ConcurrencyConflictException(string message, Exception inner) : base(message, inner) { }
}
