using System.Security.Claims;
using WebApp.Api.Auth;
using WebApp.Api.Common;
using WebApp.Application.DTOs;
using WebApp.Application.Services;
using WebApp.Domain.Enums;

namespace WebApp.Api.Endpoints;

/// <summary>
/// Image upload (multipart). Files are served back as static files under <c>/uploads</c>;
/// entities keep the returned id.
/// </summary>
internal static class ImageEndpoints
{
    /// <summary>Above the largest allowed setting (20 MB) so the setting, not Kestrel, gives the error.</summary>
    private const long MaxRequestBytes = 21 * 1024 * 1024;

    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/images").WithTags("Images").RequireAuthorization();

        // Bearer-token API: no cookie is sent with the upload, so there is nothing to forge.
        group.MapPost("/", Upload).DisableAntiforgery()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxRequestBytes))
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ImageDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Upload a JPEG / PNG / WebP (form fields: file, purpose = StoreLogo | StoreBanner | ProductPhoto | CategoryImage)");
        group.MapDelete("/{id:guid}", Delete).Produces(StatusCodes.Status204NoContent)
            .WithSummary("Delete my image (admins: any) when nothing uses it");

        return app;
    }

    private static async Task<IResult> Upload(IFormFile? file, [Microsoft.AspNetCore.Mvc.FromForm] ImagePurpose purpose,
        ImageService service, HttpContext http, CancellationToken ct)
    {
        if (Uploader(http.User) is not { } uploader) return TypedResults.Unauthorized();

        await using var stream = file?.OpenReadStream();
        var result = await service.UploadAsync(uploader, purpose, stream, file?.Length ?? 0, file?.FileName, ct);
        return result.IsSuccess ? TypedResults.Created(result.Value.Url, result.Value) : result.ToProblem();
    }

    private static async Task<IResult> Delete(Guid id, ImageService service, HttpContext http, CancellationToken ct)
    {
        if (Uploader(http.User) is not { } uploader) return TypedResults.Unauthorized();

        var result = await service.DeleteAsync(uploader, id, ct);
        return result.IsSuccess ? TypedResults.NoContent() : result.ToProblem();
    }

    private static Uploader? Uploader(ClaimsPrincipal user) =>
        user.GetUserId() is { } id
            ? new Uploader(id, user.IsInRole(nameof(Role.Admin)), user.IsInRole(nameof(Role.StoreOwner)))
            : null;
}
