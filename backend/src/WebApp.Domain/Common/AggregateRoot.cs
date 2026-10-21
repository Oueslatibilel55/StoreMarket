namespace WebApp.Domain.Common;

/// <summary>
/// Root of a consistency boundary. Carries identity and audit columns from
/// <see cref="BaseEntity"/> and collects domain events raised while the
/// aggregate mutates. Events are dispatched and cleared by the unit of work
/// when the change is committed.
/// </summary>
public abstract class AggregateRoot : BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
