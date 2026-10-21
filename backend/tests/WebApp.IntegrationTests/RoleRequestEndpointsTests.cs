using System.Net;
using System.Net.Http.Json;
using WebApp.Application.DTOs;
using WebApp.Domain.Common;
using WebApp.Domain.Enums;
using static WebApp.IntegrationTests.TestApi;

namespace WebApp.IntegrationTests;

public sealed class RoleRequestEndpointsTests(ApiFactory factory)
{
    private async Task<HttpClient> AdminClient()
    {
        var client = factory.CreateClient();
        client.WithToken((await client.SignInAdmin()).AccessToken);
        return client;
    }

    // -- Sign-up account types -----------------------------------------------------

    [Fact]
    public async Task Register_AsAffiliate_GivesAffiliateRoleAndCode_WithoutBuyer()
    {
        using var client = factory.CreateClient();

        var auth = await client.RegisterAffiliate();

        Assert.Equal([Role.Affiliate], auth.User.Roles);
        Assert.True(AffiliateCode.IsValid(auth.User.AffiliateCode));

        // The very first token already carries the role.
        client.WithToken(auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/api/me/affiliate", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task Register_AsAffiliate_WithoutTerms_Returns400()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = NewEmail(), password = Password, fullName = "Sana", phone = "55 321 987", accountType = "Affiliate" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("user.affiliateTermsRequired", await response.ProblemCode());
    }

    [Theory]
    [InlineData("StoreOwner")]
    [InlineData("Admin")]
    public async Task Register_AsStoreOwnerOrAdmin_IsRefused(string accountType)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = NewEmail(), password = Password, fullName = "Karim", phone = "22 145 678", accountType }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("user.accountTypeInvalid", await response.ProblemCode());
    }

    [Fact]
    public async Task SelfActivation_NoLongerExists()
    {
        using var client = factory.CreateClient();
        client.WithToken((await client.Register()).AccessToken);

        var response = await client.Post("/api/me/affiliate", new { acceptTerms = true });

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // -- Requests ------------------------------------------------------------------

    [Fact]
    public async Task Buyer_RequestsAffiliate_AdminApproves_CodeGivenAndAudited()
    {
        using var user = factory.CreateClient();
        var buyer = await user.Register();
        user.WithToken(buyer.AccessToken);

        var created = await user.Post("/api/me/role-requests", new { role = "Affiliate", acceptTerms = true, message = "I have many clients" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var request = await created.Read<RoleRequestDto>();
        Assert.Equal(RoleRequestStatus.Pending, request.Status);

        using var admin = await AdminClient();
        var pending = await (await admin.GetAsync(new Uri("/api/admin/role-requests?status=Pending&pageSize=100", UriKind.Relative), Ct))
            .Read<PagedResult<AdminRoleRequestItem>>();
        var item = Assert.Single(pending.Items, i => i.Id == request.Id);
        Assert.Equal(buyer.User.Email, item.UserEmail);
        Assert.Equal([Role.Buyer], item.UserRoles);

        var approved = await admin.Post($"/api/admin/role-requests/{request.Id}/approve");
        Assert.Equal(RoleRequestStatus.Approved, (await approved.Read<RoleRequestDto>()).Status);

        // After a refresh the user is Buyer + Affiliate with a code.
        var refreshed = await (await user.Post("/api/auth/refresh")).Read<AuthResponse>();
        Assert.Equal([Role.Buyer, Role.Affiliate], refreshed.User.Roles);
        Assert.True(AffiliateCode.IsValid(refreshed.User.AffiliateCode));

        var audit = await (await admin.GetAsync(new Uri($"/api/admin/audit?targetId={buyer.User.Id}", UriKind.Relative), Ct))
            .Read<PagedResult<AuditEntryDto>>();
        var entry = Assert.Single(audit.Items);
        Assert.Equal("roleRequest.approve", entry.Action);
        Assert.Equal("Affiliate", entry.Reason);
    }

    [Fact]
    public async Task Affiliate_RequestsStoreOwner_AdminApproves()
    {
        using var user = factory.CreateClient();
        var affiliate = await user.RegisterAffiliate();
        user.WithToken(affiliate.AccessToken);
        var request = await (await user.Post("/api/me/role-requests", new { role = "StoreOwner" })).Read<RoleRequestDto>();

        using var admin = await AdminClient();
        await admin.Post($"/api/admin/role-requests/{request.Id}/approve");

        var refreshed = await (await user.Post("/api/auth/refresh")).Read<AuthResponse>();
        Assert.Equal([Role.Affiliate, Role.StoreOwner], refreshed.User.Roles);
        Assert.Equal(affiliate.User.AffiliateCode, refreshed.User.AffiliateCode);
    }

    [Fact]
    public async Task AdminRefuses_WithReason_UserSeesIt_AndCanAskAgain()
    {
        using var user = factory.CreateClient();
        user.WithToken((await user.Register()).AccessToken);
        var request = await (await user.Post("/api/me/role-requests", new { role = "StoreOwner" })).Read<RoleRequestDto>();

        using var admin = await AdminClient();
        var noReason = await admin.Post($"/api/admin/role-requests/{request.Id}/refuse", new { reason = " " });
        Assert.Equal("roleRequest.reasonRequired", await noReason.ProblemCode());

        await admin.Post($"/api/admin/role-requests/{request.Id}/refuse", new { reason = "No business registration" });
        var again = await admin.Post($"/api/admin/role-requests/{request.Id}/approve");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("roleRequest.notPending", await again.ProblemCode());

        var mine = await (await user.GetAsync(new Uri("/api/me/role-requests", UriKind.Relative), Ct)).Read<List<RoleRequestDto>>();
        var refused = Assert.Single(mine);
        Assert.Equal(RoleRequestStatus.Refused, refused.Status);
        Assert.Equal("No business registration", refused.DecisionReason);

        Assert.Equal(HttpStatusCode.Created, (await user.Post("/api/me/role-requests", new { role = "StoreOwner" })).StatusCode);
    }

    [Fact]
    public async Task Request_RulesAreEnforced()
    {
        using var user = factory.CreateClient();
        user.WithToken((await user.Register()).AccessToken);

        var hasRole = await user.Post("/api/me/role-requests", new { role = "Buyer" });
        Assert.Equal(HttpStatusCode.Conflict, hasRole.StatusCode);
        Assert.Equal("roleRequest.alreadyHasRole", await hasRole.ProblemCode());

        var admin = await user.Post("/api/me/role-requests", new { role = "Admin" });
        Assert.Equal("roleRequest.roleNotRequestable", await admin.ProblemCode());

        var noTerms = await user.Post("/api/me/role-requests", new { role = "Affiliate" });
        Assert.Equal("user.affiliateTermsRequired", await noTerms.ProblemCode());

        await user.Post("/api/me/role-requests", new { role = "StoreOwner" });
        var twice = await user.Post("/api/me/role-requests", new { role = "StoreOwner" });
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal("roleRequest.alreadyPending", await twice.ProblemCode());
    }

    [Fact]
    public async Task User_CancelsOwnRequest_ButNotSomeoneElses()
    {
        using var owner = factory.CreateClient();
        owner.WithToken((await owner.Register()).AccessToken);
        var request = await (await owner.Post("/api/me/role-requests", new { role = "StoreOwner" })).Read<RoleRequestDto>();

        using var other = factory.CreateClient();
        other.WithToken((await other.Register()).AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Post($"/api/me/role-requests/{request.Id}/cancel")).StatusCode);

        var cancelled = await owner.Post($"/api/me/role-requests/{request.Id}/cancel");
        Assert.Equal(RoleRequestStatus.Cancelled, (await cancelled.Read<RoleRequestDto>()).Status);
    }

    [Fact]
    public async Task AdminEndpoints_ForbiddenForNonAdmins()
    {
        using var client = factory.CreateClient();
        client.WithToken((await client.Register()).AccessToken);

        var response = await client.GetAsync(new Uri("/api/admin/role-requests", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
