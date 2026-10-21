using WebApp.Domain.Common;

namespace WebApp.Application.Interfaces;

public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs <paramref name="work"/> in one database transaction: committed when it succeeds, rolled back when it
    /// fails (or throws). Use it when conditional updates (stock) and new rows must succeed or fail together.
    /// </summary>
    Task<Result<T>> InTransactionAsync<T>(Func<Task<Result<T>>> work, CancellationToken ct = default);
}
