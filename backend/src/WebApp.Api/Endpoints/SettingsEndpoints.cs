using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;

namespace WebApp.Api.Endpoints;

/// <summary>Platform settings: public read for the shop, admin edit (audited).</summary>
internal static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/settings", GetValues).WithTags("Settings").AllowAnonymous()
            .Produces<IReadOnlyDictionary<string, string>>()
            .WithSummary("Current value of every platform setting (key → invariant string)");

        var admin = app.MapGroup("/api/admin/settings").WithTags("Admin · Settings").RequireAuthorization(AuthPolicies.Admin);
        admin.MapGet("/", List).Produces<IReadOnlyList<AdminSettingDto>>()
            .WithSummary("Every setting with its type, default, limits and last change");
        admin.MapPut("/{key}", Update).Produces<AdminSettingDto>().ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Change one setting (validated against its type and limits, audited)");

        return app;
    }

    private static Task<IReadOnlyDictionary<string, string>> GetValues(PlatformSettingsService service, CancellationToken ct) =>
        service.GetValuesAsync(ct);

    private static Task<IReadOnlyList<AdminSettingDto>> List(PlatformSettingsService service, CancellationToken ct) =>
        service.ListAsync(ct);

    private static async Task<IResult> Update(string key, UpdateSettingRequest request, PlatformSettingsService service, HttpContext http, CancellationToken ct)
    {
        var result = await service.UpdateAsync(http.User.GetAdminActor(), key, request, ct);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.ToProblem();
    }
}
