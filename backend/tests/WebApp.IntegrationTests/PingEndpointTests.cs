using System.Net;

namespace WebApp.IntegrationTests;

public sealed class PingEndpointTests(ApiFactory factory)
{
    [Theory]
    [InlineData("/api/ping")]
    [InlineData("/alive")]
    public async Task Get_ReturnsOk(string url)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri(url, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
