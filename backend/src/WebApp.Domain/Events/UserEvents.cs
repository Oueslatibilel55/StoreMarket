using WebApp.Domain.Common;

namespace WebApp.Domain.Events;

public sealed record AffiliateActivatedEvent(Guid UserId, string AffiliateCode) : IDomainEvent;

public sealed record UserBlockedEvent(Guid UserId, string Reason) : IDomainEvent;
