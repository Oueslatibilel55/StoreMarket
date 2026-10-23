using Microsoft.EntityFrameworkCore;
using WebApp.Application.Interfaces;
using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;
using WebApp.Infrastructure.Persistence;

namespace WebApp.Infrastructure.Repositories;

internal sealed class ReviewRepository(AppDbContext db) : IReviewRepository
{
    public async Task<ReviewableStoreOrder?> FindReviewableAsync(Guid userId, string userPhone, Guid storeOrderId, CancellationToken ct = default)
    {
        var row = await (
                from o in db.Orders.AsNoTracking()
                where o.ClientId == userId || (o.ClientId == null && o.ClientPhone == userPhone && o.PlacedByAffiliateId != userId)
                from s in o.StoreOrders
                where s.Id == storeOrderId
                join st in db.Stores on s.StoreId equals st.Id
                select new
                {
                    OrderId = o.Id,
                    StoreOrderId = s.Id,
                    s.StoreId,
                    st.OwnerId,
                    s.Status,
                    Lines = s.Lines.Select(l => new ReviewableLine(l.Id, l.ProductId)).ToList(),
                })
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return row is null ? null : new ReviewableStoreOrder(row.OrderId, row.StoreOrderId, row.StoreId, row.OwnerId, row.Status, row.Lines);
    }

    public Task<bool> ExistsAsync(ReviewTarget target, Guid storeOrderId, Guid? orderLineId, CancellationToken ct = default) =>
        target == ReviewTarget.Product
            ? db.Reviews.AnyAsync(r => r.Target == ReviewTarget.Product && r.OrderLineId == orderLineId, ct)
            : db.Reviews.AnyAsync(r => r.Target == ReviewTarget.Store && r.StoreOrderId == storeOrderId, ct);

    public Task<Review?> GetAsync(Guid id, CancellationToken ct = default) => db.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct);

    public void Add(Review review) => db.Reviews.Add(review);

    public async Task RefreshRatingsAsync(Guid? productId, Guid storeId, int minReviewsPrior, CancellationToken ct = default)
    {
        // Always product before store: two transactions never wait on each other in opposite order.
        if (productId is { } pid)
        {
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM products WHERE id = {pid} FOR UPDATE", ct).ConfigureAwait(false);
            var (average, count, score) = await StatsAsync(db.Reviews.Where(r => r.Target == ReviewTarget.Product),
                r => r.ProductId == pid, minReviewsPrior, ct).ConfigureAwait(false);
            await db.Products.Where(p => p.Id == pid).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.AverageRating, average)
                .SetProperty(p => p.RatingCount, count)
                .SetProperty(p => p.RankingScore, score), ct).ConfigureAwait(false);
        }

        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM stores WHERE id = {storeId} FOR UPDATE", ct).ConfigureAwait(false);
        var store = await StatsAsync(db.Reviews.Where(r => r.Target == ReviewTarget.Store),
            r => r.StoreId == storeId, minReviewsPrior, ct).ConfigureAwait(false);
        await db.Stores.Where(s => s.Id == storeId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.AverageRating, store.Average)
            .SetProperty(x => x.RatingCount, store.Count)
            .SetProperty(x => x.RankingScore, store.Score), ct).ConfigureAwait(false);
    }

    /// <summary>The item's visible reviews against every visible review of the same kind (the prior <c>C</c>).</summary>
    private static async Task<(decimal? Average, int Count, decimal? Score)> StatsAsync(IQueryable<Review> kind,
        System.Linq.Expressions.Expression<Func<Review, bool>> item, int prior, CancellationToken ct)
    {
        var visible = kind.Where(r => r.HiddenAt == null);
        var count = await visible.Where(item).CountAsync(ct).ConfigureAwait(false);
        if (count == 0) return (null, 0, null);

        var sum = await visible.Where(item).SumAsync(r => r.Stars, ct).ConfigureAwait(false);
        var allCount = await visible.CountAsync(ct).ConfigureAwait(false);
        var allSum = await visible.SumAsync(r => r.Stars, ct).ConfigureAwait(false);
        var average = Ranking.Average(sum, count);
        return (average, count, Ranking.Score((decimal)sum / count, count, (decimal)allSum / allCount, prior));
    }
}
