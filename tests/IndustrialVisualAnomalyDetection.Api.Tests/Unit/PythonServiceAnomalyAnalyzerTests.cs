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
        string heatmapData = Convert.ToBase64String([1, 2, 3]);

        StubHttpMessageHandler handler = new(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://localhost:8000/api/v1/predictions", request.RequestUri?.AbsoluteUri);
            Assert.True(request.Headers.TryGetValues("X-Correlation-ID", out IEnumerable<string>? traceIds));
            Assert.Equal("trace-123", Assert.Single(traceIds));

            MultipartFormDataContent multipart = Assert.IsType<MultipartFormDataContent>(request.Content);

            Assert.Equal(2, multipart.Count());

            HttpContent uploadedImage = Assert.Single(
                multipart,
                content =>
                    content.Headers.ContentDisposition
                        ?.Name
                        ?.Trim('"') == "image");

            HttpContent modelIdContent = Assert.Single(
                multipart,
                content =>
                    content.Headers.ContentDisposition
                        ?.Name
                        ?.Trim('"') == "modelId");

            Assert.Equal(
                "image/png",
                uploadedImage.Headers.ContentType?.MediaType);

            byte[] uploadedBytes =
                await uploadedImage.ReadAsByteArrayAsync(
                    cancellationToken);

            string forwardedModelId =
                await modelIdContent.ReadAsStringAsync(
                    cancellationToken);

            Assert.Equal([1, 2, 3], uploadedBytes);
            Assert.Equal(
                "visa-cashew-generalized-q95-320",
                forwardedModelId);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    modelId = "mvtec-ad-capsule-320",
                    category = "capsule",
                    score = 4.992109,
                    threshold = 2.501822,
                    isAnomalous = true,
                    heatmap = new
                    {
                        contentType = "image/png",
                        width = 320,
                        height = 320,
                        dataBase64 = heatmapData
                    }
                })
            };
        });

        PythonServiceAnomalyAnalyzer analyzer = CreateAnalyzer(handler);
        using MemoryStream imageStream = new([1, 2, 3]);

        AnomalyAnalysisResult result = await analyzer.AnalyzeAsync(
            new ImageAnalysisInput(
                imageStream,
                "image/png",
                "trace-123",
                "visa-cashew-generalized-q95-320"),
            CancellationToken.None);

        Assert.Equal("mvtec-ad-capsule-320", result.ModelId);
        Assert.Equal("capsule", result.Category);
        Assert.Equal(4.992109, result.Score);
        Assert.Equal(2.501822, result.Threshold);
        Assert.True(result.IsAnomalous);
        Assert.Equal("image/png", result.Heatmap.ContentType);
        Assert.Equal(320, result.Heatmap.Width);
        Assert.Equal(320, result.Heatmap.Height);
        Assert.Equal(heatmapData, result.Heatmap.DataBase64);
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

    [Fact]
    public async Task RejectedImageIsMappedToInvalidImageContentException()
    {
        StubHttpMessageHandler handler = new((_, _) =>
        {
            HttpResponseMessage response = new(HttpStatusCode.BadRequest);
            return Task.FromResult(response);
        });

        PythonServiceAnomalyAnalyzer analyzer = CreateAnalyzer(handler);
        using MemoryStream imageStream = new([1]);

        InvalidImageContentException exception =
            await Assert.ThrowsAsync<InvalidImageContentException>(() =>
                analyzer.AnalyzeAsync(
                    new ImageAnalysisInput(imageStream, "image/png"),
                    CancellationToken.None));

        Assert.Equal(
            "The Python inference service rejected the uploaded image.",
            exception.Message);
    }

    [Fact]
    public void NullHttpClientIsRejected()
    {
        PythonInferenceOptions options = new();

        Assert.Throws<ArgumentNullException>(() =>
            new PythonServiceAnomalyAnalyzer(
                null!,
                Microsoft.Extensions.Options.Options.Create(options)));
    }

    [Fact]
    public void NullOptionsAreRejected()
    {
        using HttpClient httpClient = new();

        Assert.Throws<ArgumentNullException>(() =>
            new PythonServiceAnomalyAnalyzer(httpClient, null!));
    }

    [Fact]
    public async Task NullAnalysisInputIsRejected()
    {
        StubHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        PythonServiceAnomalyAnalyzer analyzer = CreateAnalyzer(handler);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            analyzer.AnalyzeAsync(null!, CancellationToken.None));
    }

    [Theory]
    [InlineData("", "capsule", 1.0, 0.5, true)]
    [InlineData("model", "", 1.0, 0.5, true)]
    [InlineData("model", "capsule", -1.0, 0.5, false)]
    [InlineData("model", "capsule", 1.0, -0.5, true)]
    [InlineData("model", "capsule", 1.0, 0.5, false)]
    public async Task InvalidResponseIsMappedToUnavailableException(
        string modelId,
        string category,
        double score,
        double threshold,
        bool isAnomalous)
    {
        StubHttpMessageHandler handler = new((_, _) =>
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    modelId,
                    category,
                    score,
                    threshold,
                    isAnomalous,
                    heatmap = new
                    {
                        contentType = "image/png",
                        width = 320,
                        height = 320,
                        dataBase64 = Convert.ToBase64String([1, 2, 3])
                    }
                })
            };

            return Task.FromResult(response);
        });

        PythonServiceAnomalyAnalyzer analyzer = CreateAnalyzer(handler);
        using MemoryStream imageStream = new([1]);

        InferenceUnavailableException exception =
            await Assert.ThrowsAsync<InferenceUnavailableException>(() =>
                analyzer.AnalyzeAsync(
                    new ImageAnalysisInput(imageStream, "image/png"),
                    CancellationToken.None));

        Assert.Equal(
            "The Python inference service returned an invalid response.",
            exception.Message);
    }

    [Fact]
    public async Task MissingHeatmapIsMappedToUnavailableException()
    {
        StubHttpMessageHandler handler = new((_, _) =>
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
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

            return Task.FromResult(response);
        });

        PythonServiceAnomalyAnalyzer analyzer = CreateAnalyzer(handler);
        using MemoryStream imageStream = new([1]);

        InferenceUnavailableException exception =
            await Assert.ThrowsAsync<InferenceUnavailableException>(() =>
                analyzer.AnalyzeAsync(
                    new ImageAnalysisInput(imageStream, "image/png"),
                    CancellationToken.None));

        Assert.Equal(
            "The Python inference service returned an invalid response.",
            exception.Message);
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
