using System.Net;
using WebApp.Application.DTOs;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

public sealed class RolesEndpointsTests(ApiFactory factory)
{
    [Fact]
    public async Task BootstrapAdminEmail_GetsAdminRole()
    {
        using var client = factory.CreateClient();

        var admin = await client.SignInAdmin();

        Assert.Contains(Role.Admin, admin.User.Roles);
    }

    [Fact]
    public async Task AdminEndpoints_ForbiddenForNonAdmins()
    {
        using var client = factory.CreateClient();
        var buyer = await client.Register();
        client.WithToken(buyer.AccessToken);

        var response = await client.GetAsync(new Uri($"/api/admin/users/{buyer.User.Id}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_GrantsAndRevokesRole()
    {
        using var adminClient = factory.CreateClient();
        adminClient.WithToken((await adminClient.SignInAdmin()).AccessToken);
        using var userClient = factory.CreateClient();
        var target = (await userClient.Register()).User;

        var granted = await (await adminClient.Post($"/api/admin/users/{target.Id}/roles/StoreOwner")).Read<AdminUserDto>();
        Assert.Contains(Role.StoreOwner, granted.Roles);

        var revoked = await adminClient.DeleteAsync(new Uri($"/api/admin/users/{target.Id}/roles/StoreOwner", UriKind.Relative), Ct);
        Assert.DoesNotContain(Role.StoreOwner, (await revoked.Read<AdminUserDto>()).Roles);

        var buyer = await adminClient.DeleteAsync(new Uri($"/api/admin/users/{target.Id}/roles/Buyer", UriKind.Relative), Ct);
        Assert.Equal("user.baseRoleRequired", await buyer.ProblemCode());
    }

    [Fact]
    public async Task Admin_BlocksUser_WhoIsSignedOutAndCannotSignIn()
    {
        using var adminClient = factory.CreateClient();
        adminClient.WithToken((await adminClient.SignInAdmin()).AccessToken);
        using var userClient = factory.CreateClient();
        var email = NewEmail("blocked");
        var target = (await userClient.Register(email)).User;

        var blocked = await adminClient.Post($"/api/admin/users/{target.Id}/block", new { reason = "Fake orders" });
        Assert.Equal(UserStatus.Blocked, (await blocked.Read<AdminUserDto>()).Status);

        // Existing session is gone...
        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.Post("/api/auth/refresh")).StatusCode);
        // ...and signing in again is refused.
        var login = await userClient.Post("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Equal("auth.accountBlocked", await login.ProblemCode());

        // Unblock restores access.
        await adminClient.Post($"/api/admin/users/{target.Id}/unblock");
        Assert.Equal(HttpStatusCode.OK, (await userClient.Post("/api/auth/login", new { email, password = Password })).StatusCode);
    }

    [Fact]
    public async Task Admin_CannotChangeOwnAccount()
    {
        using var client = factory.CreateClient();
        var admin = await client.SignInAdmin();
        client.WithToken(admin.AccessToken);

        var response = await client.DeleteAsync(new Uri($"/api/admin/users/{admin.User.Id}/roles/Admin", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("admin.cannotChangeOwnAccount", await response.ProblemCode());
    }
}
