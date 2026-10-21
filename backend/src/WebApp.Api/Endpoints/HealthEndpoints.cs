namespace WebApp.Api.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ping", () => TypedResults.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }))
            .WithName("Ping")
            .WithTags("Health");

        return app;
    }
}
