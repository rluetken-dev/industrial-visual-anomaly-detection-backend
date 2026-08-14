using System.Net;
using System.Net.Http.Json;
using IndustrialVisualAnomalyDetection.Api.Application.Analysis;
using IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;
using IndustrialVisualAnomalyDetection.Api.Options;

namespace IndustrialVisualAnomalyDetection.Api.Tests;

public sealed class PythonServiceAnomalyAnalyzerTests
{
    [Fact]
    public async Task SuccessfulResponseIsMappedToAnalysisResult()
    {
        StubHttpMessageHandler handler = new(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method); 
            Assert.Equal("http://localhost:8000/api/v1/predictions", request.RequestUri?.AbsoluteUri);

            MultipartFormDataContent multipart =
                Assert.IsType<MultipartFormDataContent>(request.Content);

            HttpContent uploadedImage = Assert.Single(multipart);

            Assert.Equal("image/png", uploadedImage.Headers.ContentType?.MediaType);

            Assert.Equal("image", uploadedImage.Headers.ContentDisposition?.Name?.Trim('"'));

            byte[] uploadedBytes = await uploadedImage.ReadAsByteArrayAsync(cancellationToken);

            Assert.Equal([1, 2, 3], uploadedBytes);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    modelId = "mvtec-ad-capsule-320",
                    category = "capsule",
                    score = 4.992109,
                    threshold = 2.501822,
                    isAnomalous = true
                })
            };
        });

        PythonServiceAnomalyAnalyzer analyzer = CreateAnalyzer(handler);
        using MemoryStream imageStream = new([1, 2, 3]);

        AnomalyAnalysisResult result = await analyzer.AnalyzeAsync(
            new ImageAnalysisInput(imageStream, "image/png"),
            CancellationToken.None);

        Assert.Equal("mvtec-ad-capsule-320", result.ModelId);
        Assert.Equal("capsule", result.Category);
        Assert.Equal(4.992109, result.Score);
        Assert.Equal(2.501822, result.Threshold);
        Assert.True(result.IsAnomalous);
    }

    [Fact]
    public async Task UnreachableServiceIsMappedToUnavailableException()
    {
        StubHttpMessageHandler handler = new((_, _) =>
        {
            throw new HttpRequestException("Connection refused.");
        });

        PythonServiceAnomalyAnalyzer analyzer = CreateAnalyzer(handler);
        using MemoryStream imageStream = new([1]);

        await Assert.ThrowsAsync<InferenceUnavailableException>(() =>
            analyzer.AnalyzeAsync(
                new ImageAnalysisInput(imageStream, "image/png"),
                CancellationToken.None));
    }

    [Fact]
    public async Task UnsuccessfulResponseIsMappedToUnavailableException()
    {
        StubHttpMessageHandler handler = new((_, _) =>
        {
            HttpResponseMessage response =
                new(HttpStatusCode.ServiceUnavailable);

            return Task.FromResult(response);
        });

        PythonServiceAnomalyAnalyzer analyzer = CreateAnalyzer(handler);
        using MemoryStream imageStream = new([1]);

        await Assert.ThrowsAsync<InferenceUnavailableException>(() =>
            analyzer.AnalyzeAsync(
                new ImageAnalysisInput(imageStream, "image/png"),
                CancellationToken.None));
    }

    private static PythonServiceAnomalyAnalyzer CreateAnalyzer(
        HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("http://localhost:8000")
        };

        PythonInferenceOptions options = new()
        {
            BaseUrl = "http://localhost:8000",
            PredictionPath = "/api/v1/predictions",
            TimeoutSeconds = 30
        };

        return new PythonServiceAnomalyAnalyzer(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(options));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(
            Func<
                HttpRequestMessage,
                CancellationToken,
                Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
