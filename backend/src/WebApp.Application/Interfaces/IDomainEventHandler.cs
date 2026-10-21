using WebApp.Domain.Common;

namespace WebApp.Application.Interfaces;

#pragma warning disable CA1711 // "Handler of domain events", not a .NET event delegate.
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
#pragma warning restore CA1711
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}
