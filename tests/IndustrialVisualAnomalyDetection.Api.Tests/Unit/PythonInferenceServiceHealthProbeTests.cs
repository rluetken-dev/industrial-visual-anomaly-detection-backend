using System.Net;
using IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;
using IndustrialVisualAnomalyDetection.Api.Options;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Unit;

public sealed class PythonInferenceServiceHealthProbeTests
{
    [Fact]
    public async Task Successful_health_response_is_ready()
    {
        PythonInferenceServiceHealthProbe probe = CreateProbe(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        bool result = await probe.IsReadyAsync(CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task Unsuccessful_health_response_is_not_ready()
    {
        PythonInferenceServiceHealthProbe probe = CreateProbe(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        bool result = await probe.IsReadyAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task Unreachable_service_is_not_ready()
    {
        PythonInferenceServiceHealthProbe probe = CreateProbe(
            (_, _) => throw new HttpRequestException("Service unavailable."));

        bool result = await probe.IsReadyAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task Timed_out_health_request_is_not_ready()
    {
        PythonInferenceServiceHealthProbe probe = CreateProbe(
            (_, _) => throw new TaskCanceledException("Request timed out."));

        bool result = await probe.IsReadyAsync(CancellationToken.None);

        Assert.False(result);
    }

    private static PythonInferenceServiceHealthProbe CreateProbe(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        HttpClient httpClient = new(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("http://localhost:8000")
        };

        PythonInferenceOptions options = new()
        {
            BaseUrl = "http://localhost:8000",
            HealthPath = "/health/live",
            PredictionPath = "/api/v1/predictions",
            TimeoutSeconds = 30
        };

        return new PythonInferenceServiceHealthProbe(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(options));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
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
