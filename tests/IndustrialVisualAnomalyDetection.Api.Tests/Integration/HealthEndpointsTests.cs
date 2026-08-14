using System.Net;
using System.Net.Http.Json;
using IndustrialVisualAnomalyDetection.Api.Application.Health;
using IndustrialVisualAnomalyDetection.Api.Contracts.Health;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Integration;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task Liveness_returns_healthy_response()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(false);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        HttpResponseMessage response = await client.GetAsync("/health/live");
        HealthResponse? result = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("healthy", result.Status);
    }

    [Fact]
    public async Task Readiness_returns_ready_when_inference_service_is_available()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(true);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        HttpResponseMessage response = await client.GetAsync("/health/ready");
        HealthResponse? result = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("ready", result.Status);
    }

    [Fact]
    public async Task Readiness_returns_service_unavailable_when_inference_service_is_unavailable()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(false);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        HttpResponseMessage response = await client.GetAsync("/health/ready");
        HealthResponse? result = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal("not_ready", result.Status);
    }

    private static WebApplicationFactory<Program> CreateFactory(bool isReady)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInferenceServiceHealthProbe>();
                services.AddSingleton<IInferenceServiceHealthProbe>(
                    new StubInferenceServiceHealthProbe(isReady));
            });
        });
    }

    private sealed class StubInferenceServiceHealthProbe : IInferenceServiceHealthProbe
    {
        private readonly bool _isReady;

        public StubInferenceServiceHealthProbe(bool isReady)
        {
            _isReady = isReady;
        }

        public Task<bool> IsReadyAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_isReady);
        }
    }
}
