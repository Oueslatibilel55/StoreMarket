using System.Net;
using System.Text.Json;
using WebApp.Application.DTOs;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

public sealed class AdminPanelTests(ApiFactory factory)
{
    private async Task<HttpClient> AdminClient()
    {
        var client = factory.CreateClient();
        client.WithToken((await client.SignInAdmin()).AccessToken);
        return client;
    }

    private async Task<UserDto> NewUser(string email)
    {
        using var client = factory.CreateClient();
        return (await client.Register(email)).User;
    }

    private static Task<PagedResult<AdminUserListItem>> List(HttpClient client, string query) =>
        client.GetAsync(new Uri($"/api/admin/users?{query}", UriKind.Relative), Ct).ContinueWith(t => t.Result.Read<PagedResult<AdminUserListItem>>()).Unwrap();

    [Fact]
    public async Task ListUsers_SearchesByEmailAndName_CaseInsensitive()
    {
        using var admin = await AdminClient();
        var tag = Guid.NewGuid().ToString("N")[..10];
        await NewUser($"search-{tag}@example.com");

        var byEmail = await List(admin, $"search={tag.ToUpperInvariant()}");

        var hit = Assert.Single(byEmail.Items);
        Assert.Equal($"search-{tag}@example.com", hit.Email);
        Assert.Equal(1, byEmail.Total);
    }

    [Fact]
    public async Task ListUsers_FiltersByRoleAndStatus_AndPages()
    {
        using var admin = await AdminClient();
        var tag = Guid.NewGuid().ToString("N")[..10];
        var users = new List<UserDto>();
        for (var i = 0; i < 3; i++) users.Add(await NewUser($"page{i}-{tag}@example.com"));

        await admin.Post($"/api/admin/users/{users[0].Id}/roles/StoreOwner");
        await admin.Post($"/api/admin/users/{users[1].Id}/block", new { reason = "test" });

        var owners = await List(admin, $"search={tag}&role=StoreOwner");
        Assert.Equal(users[0].Id, Assert.Single(owners.Items).Id);

        var blocked = await List(admin, $"search={tag}&status=Blocked");
        Assert.Equal(users[1].Id, Assert.Single(blocked.Items).Id);

        var page1 = await List(admin, $"search={tag}&pageSize=2&page=1");
        var page2 = await List(admin, $"search={tag}&pageSize=2&page=2");
        Assert.Equal(3, page1.Total);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(2, page1.Items.Count);
        Assert.Single(page2.Items);
        Assert.Empty(page1.Items.Select(u => u.Id).Intersect(page2.Items.Select(u => u.Id)));
    }

    [Fact]
    public async Task ListUsers_SearchesByPhoneDigits()
    {
        using var admin = await AdminClient();
        var tag = Guid.NewGuid().ToString("N")[..10];
        var user = await NewUser($"phone-{tag}@example.com");

        var result = await List(admin, $"search=22%20123%20456&pageSize=100");

        Assert.Contains(result.Items, u => u.Id == user.Id);
    }

    [Fact]
    public async Task Block_WritesAuditEntryWithReasonAndBeforeAfter()
    {
        using var admin = await AdminClient();
        var user = await NewUser(NewEmail("audit"));

        await admin.Post($"/api/admin/users/{user.Id}/block", new { reason = "Fake orders" });
        var audit = await (await admin.GetAsync(new Uri($"/api/admin/audit?targetType=User&targetId={user.Id}", UriKind.Relative), Ct))
            .Read<PagedResult<AuditEntryDto>>();

        var entry = Assert.Single(audit.Items);
        Assert.Equal("user.block", entry.Action);
        Assert.Equal("Fake orders", entry.Reason);
        Assert.Equal(TestApi.AdminEmail, entry.ActorEmail);
        Assert.Equal(user.Email, entry.TargetLabel);
        Assert.Equal("Active", JsonDocument.Parse(entry.Before!).RootElement.GetProperty("status").GetString());
        Assert.Equal("Blocked", JsonDocument.Parse(entry.After!).RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task NoOpChange_IsNotAudited()
    {
        using var admin = await AdminClient();
        var user = await NewUser(NewEmail("noop"));

        await admin.Post($"/api/admin/users/{user.Id}/roles/StoreOwner");
        await admin.Post($"/api/admin/users/{user.Id}/roles/StoreOwner"); // already has it
        await admin.Post($"/api/admin/users/{user.Id}/unblock");          // already active

        var audit = await (await admin.GetAsync(new Uri($"/api/admin/audit?targetId={user.Id}", UriKind.Relative), Ct))
            .Read<PagedResult<AuditEntryDto>>();
        Assert.Equal("user.role.grant", Assert.Single(audit.Items).Action);
    }

    [Fact]
    public async Task FailedChange_IsNotAudited()
    {
        using var admin = await AdminClient();
        var user = await NewUser(NewEmail("fail"));

        var response = await admin.DeleteAsync(new Uri($"/api/admin/users/{user.Id}/roles/Buyer", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var audit = await (await admin.GetAsync(new Uri($"/api/admin/audit?targetId={user.Id}", UriKind.Relative), Ct))
            .Read<PagedResult<AuditEntryDto>>();
        Assert.Empty(audit.Items);
    }

    [Fact]
    public async Task Counts_ReflectRolesAndStatus()
    {
        using var admin = await AdminClient();

        var counts = await (await admin.GetAsync(new Uri("/api/admin/users/counts", UriKind.Relative), Ct)).Read<UserCounts>();

        Assert.True(counts.Total >= 1);
        Assert.True(counts.Admins >= 1);
        Assert.Equal(counts.Total, counts.Active + counts.Blocked);
    }

    [Theory]
    [InlineData("/api/admin/users")]
    [InlineData("/api/admin/users/counts")]
    [InlineData("/api/admin/audit")]
    public async Task AdminReads_AreForbiddenForBuyers(string url)
    {
        using var client = factory.CreateClient();
        client.WithToken((await client.Register()).AccessToken);

        var response = await client.GetAsync(new Uri(url, UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
