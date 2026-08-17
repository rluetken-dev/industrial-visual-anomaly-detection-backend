using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using IndustrialVisualAnomalyDetection.Api.Application.Analysis;
using IndustrialVisualAnomalyDetection.Api.Contracts.Analyses;
using IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Integration;

public sealed class AnalysesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly byte[] ValidPngContent =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
    ];

    private readonly WebApplicationFactory<Program> _factory;

    public AnalysesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MissingImageReturnsBadRequest()
    {
        using HttpClient client = CreateClient(_factory);
        using MultipartFormDataContent content = new();

        using HttpResponseMessage response = await client.PostAsync("/api/v1/analyses", content);

        ProblemDetails problem = await ReadProblemDetails(response, HttpStatusCode.BadRequest);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
    }

    [Fact]
    public async Task EmptyImageReturnsBadRequest()
    {
        using HttpClient client = CreateClient(_factory);
        using MultipartFormDataContent content = CreateUpload([], "image/png");

        using HttpResponseMessage response = await client.PostAsync("/api/v1/analyses", content);

        ProblemDetails problem = await ReadProblemDetails(response, HttpStatusCode.BadRequest);
        Assert.Equal("Invalid image", problem.Title);
    }

    [Fact]
    public async Task UnsupportedContentTypeReturnsUnsupportedMediaType()
    {
        using HttpClient client = CreateClient(_factory);
        using MultipartFormDataContent content = CreateUpload([1], "image/gif");

        using HttpResponseMessage response = await client.PostAsync("/api/v1/analyses", content);

        ProblemDetails problem = await ReadProblemDetails(response, HttpStatusCode.UnsupportedMediaType);
        Assert.Equal("Unsupported image type", problem.Title);
    }

    [Fact]
    public async Task OversizedImageReturnsPayloadTooLarge()
    {
        using WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ImageUpload:MaxFileSizeBytes"] = "4"
                });
            });
        });

        using HttpClient client = CreateClient(factory);
        using MultipartFormDataContent content = CreateUpload([1, 2, 3, 4, 5], "image/png");

        using HttpResponseMessage response = await client.PostAsync("/api/v1/analyses", content);

        ProblemDetails problem = await ReadProblemDetails(
            response,
            HttpStatusCode.RequestEntityTooLarge);

        Assert.Equal("Image too large", problem.Title);
    }

    [Fact]
    public async Task ValidUploadReturnsServiceUnavailableWithoutInferenceAdapter()
    {
        using WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAnomalyAnalyzer>();
                services.AddScoped<IAnomalyAnalyzer, UnavailableAnomalyAnalyzer>();
            });
        });

        using HttpClient client = CreateClient(factory);
        using MultipartFormDataContent content = CreateUpload(ValidPngContent, "image/png");

        using HttpResponseMessage response = await client.PostAsync("/api/v1/analyses", content);

        ProblemDetails problem = await ReadProblemDetails(
            response,
            HttpStatusCode.ServiceUnavailable);

        Assert.Equal("Inference unavailable", problem.Title);
        Assert.Equal(
            "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/inference-unavailable",
            problem.Type);
    }

    [Fact]
    public async Task UnreadableImageReturnsBadRequest()
    {
        using WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAnomalyAnalyzer>();
                services.AddSingleton<IAnomalyAnalyzer>(new InvalidImageAnomalyAnalyzer());
            });
        });

        using HttpClient client = CreateClient(factory);
        using MultipartFormDataContent content = CreateUpload(ValidPngContent, "image/png");

        using HttpResponseMessage response = await client.PostAsync("/api/v1/analyses", content);

        ProblemDetails problem = await ReadProblemDetails(
            response,
            HttpStatusCode.BadRequest);

        Assert.Equal("Invalid image", problem.Title);
        Assert.Equal("The uploaded file is not a readable image.", problem.Detail);
        Assert.Equal(
            "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/invalid-image",
            problem.Type);
    }

    [Fact]
    public async Task SuccessfulAnalysisReturnsMappedResponse()
    {
        AnomalyAnalysisResult analysisResult = new(
            "mvtec-ad-capsule-320",
            "capsule",
            4.992109,
            2.501822,
            true,
            new AnomalyHeatmap(
                "image/png",
                320,
                320,
                Convert.ToBase64String([1, 2, 3])));

        using WebApplicationFactory<Program> factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAnomalyAnalyzer>();
                services.AddSingleton<IAnomalyAnalyzer>(new StubAnomalyAnalyzer(analysisResult));
            });
        });

        using HttpClient client = CreateClient(factory);
        using MultipartFormDataContent content = CreateUpload(ValidPngContent, "image/png");

        using HttpResponseMessage response = await client.PostAsync("/api/v1/analyses", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        AnalysisResponse? result = await response.Content.ReadFromJsonAsync<AnalysisResponse>();

        Assert.NotNull(result);
        Assert.Equal("mvtec-ad-capsule-320", result.Model.Id);
        Assert.Equal("capsule", result.Model.Category);
        Assert.Equal(4.992109, result.Score);
        Assert.Equal(2.501822, result.Threshold);
        Assert.Equal("anomalous", result.Decision);
        Assert.True(result.ProcessingTimeMs >= 0);
        Assert.False(string.IsNullOrWhiteSpace(result.TraceId));
        Assert.Equal("image/png", result.Heatmap.ContentType);
        Assert.Equal(320, result.Heatmap.Width);
        Assert.Equal(320, result.Heatmap.Height);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), result.Heatmap.DataBase64);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        return factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    private static MultipartFormDataContent CreateUpload(byte[] content, string contentType)
    {
        ByteArrayContent imageContent = new(content);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        MultipartFormDataContent form = new();
        form.Add(imageContent, "image", "test-image.bin");

        return form;
    }

    private static async Task<ProblemDetails> ReadProblemDetails(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode)
    {
        Assert.Equal(expectedStatusCode, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problem);
        Assert.True(problem.Extensions.ContainsKey("traceId"));

        return problem;
    }

    private sealed class StubAnomalyAnalyzer : IAnomalyAnalyzer
    {
        private readonly AnomalyAnalysisResult _result;

        public StubAnomalyAnalyzer(AnomalyAnalysisResult result)
        {
            _result = result;
        }

        public Task<AnomalyAnalysisResult> AnalyzeAsync(
            ImageAnalysisInput input,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class InvalidImageAnomalyAnalyzer : IAnomalyAnalyzer
    {
        public Task<AnomalyAnalysisResult> AnalyzeAsync(
            ImageAnalysisInput input,
            CancellationToken cancellationToken)
        {
            return Task.FromException<AnomalyAnalysisResult>(
                new InvalidImageContentException(
                    "The Python inference service rejected the uploaded image."));
        }
    }
}
