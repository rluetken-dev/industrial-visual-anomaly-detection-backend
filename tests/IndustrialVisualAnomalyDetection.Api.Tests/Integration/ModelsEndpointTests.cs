using System.Net;
using System.Net.Http.Json;
using IndustrialVisualAnomalyDetection.Api.Application.Analysis;
using IndustrialVisualAnomalyDetection.Api.Application.Models;
using IndustrialVisualAnomalyDetection.Api.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Integration;

public sealed class ModelsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ModelsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SuccessfulCatalogReturnsMappedResponse()
    {
        InferenceModelCatalog catalog = new(
            "capsule",
            [
                new InferenceModelDescriptor(
                    "capsule",
                    "MVTec AD - Capsule",
                    "capsule",
                    320,
                    true),
                new InferenceModelDescriptor(
                    "cashew",
                    "VisA - Cashew",
                    "cashew",
                    320,
                    false)
            ]);

        StubInferenceModelCatalogProvider provider = new(
            _ => Task.FromResult(catalog));

        using WebApplicationFactory<Program> factory = CreateFactory(provider);
        using HttpClient client = CreateClient(factory);

        using HttpResponseMessage response =
            await client.GetAsync("/api/v1/models");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        InferenceModelCatalogResponse? result =
            await response.Content.ReadFromJsonAsync<InferenceModelCatalogResponse>();

        Assert.NotNull(result);
        Assert.Equal("capsule", result.DefaultModelId);
        Assert.Collection(
            result.Models,
            model =>
            {
                Assert.Equal("capsule", model.Id);
                Assert.Equal("MVTec AD - Capsule", model.DisplayName);
                Assert.Equal("capsule", model.Category);
                Assert.Equal(320, model.InputSize);
                Assert.True(model.IsDefault);
            },
            model =>
            {
                Assert.Equal("cashew", model.Id);
                Assert.Equal("VisA - Cashew", model.DisplayName);
                Assert.Equal("cashew", model.Category);
                Assert.Equal(320, model.InputSize);
                Assert.False(model.IsDefault);
            });
    }

    [Fact]
    public async Task UnavailableCatalogReturnsServiceUnavailable()
    {
        StubInferenceModelCatalogProvider provider = new(
            _ => Task.FromException<InferenceModelCatalog>(
                new InferenceUnavailableException(
                    "The Python inference service could not be reached.")));

        using WebApplicationFactory<Program> factory = CreateFactory(provider);
        using HttpClient client = CreateClient(factory);

        using HttpResponseMessage response =
            await client.GetAsync("/api/v1/models");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        ProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.Equal("Inference unavailable", problem.Title);
        Assert.Equal(
            "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/inference-unavailable",
            problem.Type);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    private WebApplicationFactory<Program> CreateFactory(
        IInferenceModelCatalogProvider provider)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInferenceModelCatalogProvider>();
                services.AddSingleton(provider);
            });
        });
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    private sealed class StubInferenceModelCatalogProvider
        : IInferenceModelCatalogProvider
    {
        private readonly Func<
            CancellationToken,
            Task<InferenceModelCatalog>> _handler;

        public StubInferenceModelCatalogProvider(
            Func<CancellationToken, Task<InferenceModelCatalog>> handler)
        {
            _handler = handler;
        }

        public Task<InferenceModelCatalog> GetCatalogAsync(
            CancellationToken cancellationToken)
        {
            return _handler(cancellationToken);
        }
    }
}
