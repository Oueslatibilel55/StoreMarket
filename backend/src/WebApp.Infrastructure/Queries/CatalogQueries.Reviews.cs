using Microsoft.EntityFrameworkCore;
using WebApp.Application.DTOs;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.Infrastructure.Queries;

/// <summary>Public reviews on product and store pages (017): visible reviews only, newest first.</summary>
internal sealed partial class CatalogQueries
{
    public async Task<PublicReviewsDto?> GetProductReviewsAsync(string productSlug, int? stars, int? page, int? pageSize, CancellationToken ct = default)
    {
        var productId = await VisibleProducts().Where(p => p.Slug == productSlug).Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (productId is null) return null;
        return await PublicReviewsAsync(db.Reviews.Where(r => r.Target == ReviewTarget.Product && r.ProductId == productId), stars, page, pageSize, ct)
            .ConfigureAwait(false);
    }

    public async Task<PublicReviewsDto?> GetStoreReviewsAsync(string storeSlug, int? stars, int? page, int? pageSize, CancellationToken ct = default)
    {
        var storeId = await db.Stores.AsNoTracking()
            .Where(s => s.Slug == storeSlug && (s.Status == StoreStatus.Active || s.Status == StoreStatus.Closed))
            .Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (storeId is null) return null;
        return await PublicReviewsAsync(db.Reviews.Where(r => r.Target == ReviewTarget.Store && r.StoreId == storeId), stars, page, pageSize, ct)
            .ConfigureAwait(false);
    }

    private static async Task<PublicReviewsDto> PublicReviewsAsync(IQueryable<Review> reviews, int? stars, int? page, int? pageSize, CancellationToken ct)
    {
        var paging = Paging.From(page, pageSize);
        var visible = reviews.AsNoTracking().Where(r => r.HiddenAt == null);
        var stats = await ReviewStats.ForAsync(visible, ct).ConfigureAwait(false);

        var filtered = stars is >= 1 and <= 5 ? visible.Where(r => r.Stars == stars) : visible;
        var total = await filtered.CountAsync(ct).ConfigureAwait(false);
        var items = await filtered.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
            .Skip(paging.Skip).Take(paging.PageSize)
            .Select(r => new PublicReviewDto(r.Id, r.AuthorName, r.Stars, r.Comment, r.CreatedAt, r.EditedAt, r.Reply, r.RepliedAt))
            .ToListAsync(ct).ConfigureAwait(false);
        return new PublicReviewsDto(stats, new PagedResult<PublicReviewDto>(items, paging.Page, paging.PageSize, total));
    }
}

/// <summary>Average, count and 1★…5★ breakdown of a set of reviews (callers pass the visible ones).</summary>
internal static class ReviewStats
{
    public static async Task<ReviewStatsDto> ForAsync(IQueryable<Review> reviews, CancellationToken ct)
    {
        var groups = await reviews.GroupBy(r => r.Stars).Select(g => new { Stars = g.Key, Count = g.Count() })
            .ToListAsync(ct).ConfigureAwait(false);
        var byStars = Enumerable.Range(1, 5).Select(s => groups.Where(g => g.Stars == s).Sum(g => g.Count)).ToList();
        var count = byStars.Sum();
        var sum = groups.Sum(g => g.Stars * g.Count);
        return new ReviewStatsDto(Domain.Common.Ranking.Average(sum, count), count, byStars);
    }
}
