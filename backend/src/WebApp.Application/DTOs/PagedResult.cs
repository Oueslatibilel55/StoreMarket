namespace WebApp.Application.DTOs;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

/// <summary>Page/size from a query string, clamped to sane values.</summary>
public readonly record struct Paging(int Page, int PageSize)
{
    public const int MaxPageSize = 100;

    public static Paging From(int? page, int? pageSize) =>
        new(Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? 20, 1, MaxPageSize));

    public int Skip => (Page - 1) * PageSize;
}
