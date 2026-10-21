using WebApp.Application.Interfaces;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Application.Payments;

/// <summary>Cash on delivery (015): the courier collects <c>AmountToCollect</c>; delivered means paid.</summary>
public sealed class CashOnDelivery : IPaymentMethod
{
    public PaymentMethod Method => PaymentMethod.CashOnDelivery;

    public void OnDelivered(StoreOrder storeOrder, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(storeOrder);
        storeOrder.MarkPaid(utcNow);
    }
}
