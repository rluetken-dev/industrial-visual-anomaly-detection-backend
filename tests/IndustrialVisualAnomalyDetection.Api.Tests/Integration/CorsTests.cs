using IndustrialVisualAnomalyDetection.Api.Options;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Integration;

public sealed class CorsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string AllowedOrigin = "https://localhost:5173";

    private readonly WebApplicationFactory<Program> _factory;

    public CorsTests(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _factory = factory;
    }

    [Fact]
    public async Task ConfiguredOriginIsAllowed()
    {
        using WebApplicationFactory<Program> factory = CreateFactoryWithOrigin(AllowedOrigin);
        using HttpClient client = CreateClient(factory);
        using HttpRequestMessage request = CreatePreflightRequest(AllowedOrigin);

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues(
            "Access-Control-Allow-Origin",
            out IEnumerable<string>? origins));

        Assert.Equal(AllowedOrigin, Assert.Single(origins));
    }

    [Fact]
    public async Task UnconfiguredOriginIsNotAllowed()
    {
        using HttpClient client = CreateClient(_factory);
        using HttpRequestMessage request = CreatePreflightRequest("https://untrusted.example");

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public void OriginWithPathIsRejected()
    {
        using WebApplicationFactory<Program> factory =
            CreateFactoryWithOrigin("https://localhost:5173/application");

        Assert.Throws<OptionsValidationException>(() =>
        {
            using IServiceScope scope = factory.Services.CreateScope();

            _ = scope.ServiceProvider
                .GetRequiredService<IOptions<ApiCorsOptions>>()
                .Value;
        });
    }

    private WebApplicationFactory<Program> CreateFactoryWithOrigin(string origin)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cors:AllowedOrigins:0"] = origin
                });
            });
        });
    }

    private static HttpRequestMessage CreatePreflightRequest(string origin)
    {
        HttpRequestMessage request = new(HttpMethod.Options, "/api/v1/analyses");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        return request;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }
}
