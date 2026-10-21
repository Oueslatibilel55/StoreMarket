using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;

namespace WebApp.Infrastructure.Events;

/// <summary>
/// In-process dispatcher. Resolves every <see cref="IDomainEventHandler{TEvent}"/>
/// registered for the concrete event type and invokes it.
/// </summary>
public sealed partial class DomainEventDispatcher(
    IServiceProvider provider,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, (Type HandlerType, MethodInfo Handle)> Cache = new();

    public async Task DispatchAsync(IReadOnlyList<IDomainEvent> events, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var domainEvent in events)
        {
            var (handlerType, handle) = Cache.GetOrAdd(domainEvent.GetType(), eventType =>
            {
                var closed = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
                return (closed, closed.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!);
            });

            foreach (var handler in provider.GetServices(handlerType))
            {
                if (handler is null) continue;

                try
                {
                    await ((Task)handle.Invoke(handler, [domainEvent, ct])!).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // The change is already committed; a failing side effect must not fail the request.
                catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
                {
                    LogHandlerFailed(logger, ex, handler.GetType().Name, domainEvent.GetType().Name);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Domain event handler {Handler} failed for {EventType}")]
    private static partial void LogHandlerFailed(ILogger logger, Exception ex, string handler, string eventType);
}
