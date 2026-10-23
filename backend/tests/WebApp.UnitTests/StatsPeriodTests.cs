using WebApp.Application.DTOs;

namespace WebApp.UnitTests;

public sealed class StatsPeriodTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    [Fact]
    public void Default_IsTheLast30Days_UpToToday()
    {
        var p = StatsPeriod.Of(null, null, Today);

        Assert.Equal(new DateOnly(2026, 9, 24), p.From);
        Assert.Equal(Today, p.To);
        Assert.Equal(StatsPeriod.DefaultDays, p.Days);
        Assert.Equal(new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc), p.Start);
        Assert.Equal(new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc), p.End); // the last day is included
    }

    [Fact]
    public void OneEnd_GetsTheOtherByDefault_ReversedEndsAreSwapped()
    {
        Assert.Equal(new DateOnly(2026, 1, 1), StatsPeriod.Of(new DateOnly(2026, 1, 1), null, Today).From);
        Assert.Equal(Today, StatsPeriod.Of(new DateOnly(2026, 1, 1), null, Today).To);
        Assert.Equal(new DateOnly(2026, 5, 2), StatsPeriod.Of(null, new DateOnly(2026, 5, 31), Today).From);

        var swapped = StatsPeriod.Of(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 1), Today);
        Assert.Equal((new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 10)), (swapped.From, swapped.To));
        Assert.Equal(1, StatsPeriod.Of(Today, Today, Today).Days);
    }

    [Fact]
    public void TooLong_KeepsTheLastDays()
    {
        var p = StatsPeriod.Of(new DateOnly(2020, 1, 1), Today, Today);

        Assert.Equal(StatsPeriod.MaxDays, p.Days);
        Assert.Equal(Today, p.To);
    }
}
