using WebApp.Domain.Common;
using WebApp.Domain.Entities;
using WebApp.Domain.Enums;

namespace WebApp.UnitTests;

public sealed class ReviewTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AuthorId = Guid.CreateVersion7();
    private static readonly Guid StoreId = Guid.CreateVersion7();

    private static Result<Review> Product(int stars = 4, string? comment = "  Très bon parfum  ", int window = 30) =>
        Review.ForProduct(AuthorId, "Leila Ben Gharbi", Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), StoreId, stars, comment, Now, window);

    [Fact]
    public void ForProduct_KeepsTheTargets_TrimsComment_FreezesEditWindow_AndDisplayName()
    {
        var r = Product().Value;

        Assert.Equal(ReviewTarget.Product, r.Target);
        Assert.NotNull(r.OrderLineId);
        Assert.NotNull(r.ProductId);
        Assert.Equal("Très bon parfum", r.Comment);
        Assert.Equal("Leila G.", r.AuthorName);
        Assert.Equal(Now.AddDays(30), r.EditableUntil);
        Assert.Null(r.EditedAt);
    }

    [Fact]
    public void ForStore_HasNoProduct_AndAnEmptyCommentIsNull()
    {
        var r = Review.ForStore(AuthorId, "Karim", Guid.CreateVersion7(), Guid.CreateVersion7(), StoreId, 5, "   ", Now, 30).Value;

        Assert.Equal(ReviewTarget.Store, r.Target);
        Assert.Null(r.OrderLineId);
        Assert.Null(r.ProductId);
        Assert.Null(r.Comment);
        Assert.Equal("Karim", r.AuthorName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Stars_OutsideOneToFive_AreRefused(int stars) =>
        Assert.Equal(DomainErrors.Review.StarsInvalid, Product(stars).Error);

    [Fact]
    public void Comment_TooLong_IsRefused() =>
        Assert.Equal(DomainErrors.Review.CommentTooLong, Product(comment: new string('a', Review.MaxCommentLength + 1)).Error);

    [Fact]
    public void Edit_WithinTheWindow_ChangesStarsAndComment_AfterIt_IsRefused()
    {
        var r = Product().Value;

        Assert.True(r.Edit(2, "Finalement déçue", Now.AddDays(10)).IsSuccess);
        Assert.Equal((2, "Finalement déçue"), (r.Stars, r.Comment));
        Assert.Equal(Now.AddDays(10), r.EditedAt);

        Assert.Equal(DomainErrors.Review.EditWindowClosed, r.Edit(5, null, Now.AddDays(31)).Error);
        Assert.Equal(2, r.Stars);
        Assert.False(r.CanEdit(Now.AddDays(31)));
    }

    [Fact]
    public void Edit_WithZeroDayWindow_IsClosedRightAway()
    {
        var r = Product(window: 0).Value;
        Assert.Equal(DomainErrors.Review.EditWindowClosed, r.Edit(3, null, Now.AddMinutes(1)).Error);
    }

    [Fact]
    public void Edit_SameContent_IsANoOp()
    {
        var r = Product().Value;
        Assert.True(r.Edit(4, "Très bon parfum", Now.AddDays(1)).IsSuccess);
        Assert.Null(r.EditedAt);
    }

    [Fact]
    public void Reply_IsTrimmedAndRequired_AndCanBeRemoved()
    {
        var r = Product().Value;

        Assert.Equal(DomainErrors.Review.ReplyRequired, r.SetReply("  ", Now).Error);
        Assert.Equal(DomainErrors.Review.ReplyTooLong, r.SetReply(new string('a', Review.MaxReplyLength + 1), Now).Error);
        Assert.True(r.SetReply("  Merci Leila !  ", Now).IsSuccess);
        Assert.Equal(("Merci Leila !", Now), (r.Reply, r.RepliedAt));

        r.RemoveReply();
        Assert.Null(r.Reply);
        Assert.Null(r.RepliedAt);
    }

    [Fact]
    public void Hide_NeedsAReason_BlocksEditAndReply_UnhideBringsItBack()
    {
        var r = Product().Value;

        Assert.Equal(DomainErrors.Review.ReasonRequired, r.Hide(" ", Now).Error);
        Assert.True(r.Hide(" Insultes ", Now).IsSuccess);
        Assert.True(r.IsHidden);
        Assert.Equal("Insultes", r.HiddenReason);
        Assert.Equal(DomainErrors.Review.AlreadyHidden, r.Hide("Encore", Now).Error);
        Assert.Equal(DomainErrors.Review.Hidden, r.Edit(5, null, Now).Error);
        Assert.Equal(DomainErrors.Review.Hidden, r.SetReply("Merci", Now).Error);
        Assert.False(r.CanEdit(Now));

        Assert.Equal(DomainErrors.Review.ReasonRequired, r.Unhide(null).Error);
        Assert.True(r.Unhide("Signalement abusif").IsSuccess);
        Assert.False(r.IsHidden);
        Assert.Null(r.HiddenReason);
        Assert.Equal(DomainErrors.Review.NotHidden, r.Unhide("Encore").Error);
    }

    [Theory]
    [InlineData("Leila Gharbi", "Leila G.")]
    [InlineData("  amel   ben  zouari ", "amel Z.")]
    [InlineData("Sana", "Sana")]
    [InlineData("", "?")]
    public void DisplayName_IsFirstNameAndInitial(string full, string expected) =>
        Assert.Equal(expected, Review.DisplayName(full));

    [Fact]
    public void Ranking_ManyGoodReviews_BeatOnePerfectOne()
    {
        const decimal global = 3.8m;
        var many = Ranking.Score(4.8m, 200, global, 10);
        var one = Ranking.Score(5m, 1, global, 10);

        Assert.True(many > one);
        Assert.Equal(3.9091m, one); // (1 × 5 + 10 × 3.8) / 11
        Assert.Null(Ranking.Score(null, 0, global, 10));
        Assert.Equal(5m, Ranking.Score(5m, 1, global, 0)); // no prior: the plain average
    }

    [Fact]
    public void Average_HasTwoDecimals_AndIsNullWithoutReviews()
    {
        Assert.Equal(4.67m, Ranking.Average(14, 3));
        Assert.Null(Ranking.Average(0, 0));
    }
}
