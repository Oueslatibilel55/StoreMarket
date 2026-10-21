using Microsoft.EntityFrameworkCore;
using WebApp.Application.Common;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;

namespace WebApp.Infrastructure.Persistence;

public sealed class UnitOfWork(AppDbContext db, IDomainEventDispatcher dispatcher) : IUnitOfWork
{
    /// <summary>
    /// Saves the change and then dispatches whatever domain events the tracked
    /// aggregates raised. Doing it here means every command path publishes its
    /// events — services cannot forget to.
    /// </summary>
    public async Task<int> CommitAsync(CancellationToken ct = default)
    {
        var roots = db.ChangeTracker
            .Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var events = roots.SelectMany(r => r.DomainEvents).ToList();

        int affected;
        try
        {
            affected = await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The record changed since it was read.", ex);
        }

        if (events.Count > 0)
        {
            // Cleared before dispatch so a handler that commits again cannot
            // republish the same events.
            foreach (var root in roots) root.ClearDomainEvents();
            await dispatcher.DispatchAsync(events, ct).ConfigureAwait(false);
        }

        return affected;
    }

    public async Task<Result<T>> InTransactionAsync<T>(Func<Task<Result<T>>> work, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var result = await work().ConfigureAwait(false);
        if (result.IsSuccess) await transaction.CommitAsync(ct).ConfigureAwait(false);
        else
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            // Whatever the failed work added or changed must not be saved by a later commit.
            db.ChangeTracker.Clear();
        }
        return result;
    }
}
