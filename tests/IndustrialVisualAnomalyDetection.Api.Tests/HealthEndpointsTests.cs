using System.Net;
using System.Net.Http.Json;
using IndustrialVisualAnomalyDetection.Api.Contracts.Health;
using Microsoft.AspNetCore.Mvc.Testing;

namespace IndustrialVisualAnomalyDetection.Api.Tests;

public sealed class HealthEndpointsTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Liveness_returns_healthy_response()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        HttpResponseMessage response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        HealthResponse? result = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.NotNull(result);
        Assert.Equal("healthy", result.Status);
    }

    [Fact]
    public async Task Readiness_returns_ready_response()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        HttpResponseMessage response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        HealthResponse? result = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.NotNull(result);
        Assert.Equal("ready", result.Status);
    }
}
