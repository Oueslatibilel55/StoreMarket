using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.Interfaces;

/// <summary>
/// How a store order gets paid (015). v1 has cash on delivery only; an online gateway would be another
/// implementation, without touching orders.
/// </summary>
public interface IPaymentMethod
{
    PaymentMethod Method { get; }

    /// <summary>The store confirmed delivery. Cash on delivery: the cash was handed over, the order is paid.</summary>
    void OnDelivered(StoreOrder storeOrder, DateTime utcNow);
}
