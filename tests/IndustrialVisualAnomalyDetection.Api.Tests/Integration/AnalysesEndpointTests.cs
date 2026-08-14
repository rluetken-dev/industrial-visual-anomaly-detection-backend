using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Integration;

public sealed class AnalysesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
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
        using HttpClient client = CreateClient(_factory);
        using MultipartFormDataContent content = CreateUpload([1], "image/png");

        using HttpResponseMessage response = await client.PostAsync("/api/v1/analyses", content);

        ProblemDetails problem = await ReadProblemDetails(
            response,
            HttpStatusCode.ServiceUnavailable);

        Assert.Equal("Inference unavailable", problem.Title);
        Assert.Equal(
            "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/inference-unavailable",
            problem.Type);
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
}
