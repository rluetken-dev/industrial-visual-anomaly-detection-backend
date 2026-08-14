using IndustrialVisualAnomalyDetection.Api.Application.Health;
using IndustrialVisualAnomalyDetection.Api.Options;
using Microsoft.Extensions.Options;

namespace IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;

public sealed class PythonInferenceServiceHealthProbe : IInferenceServiceHealthProbe
{
    private readonly HttpClient _httpClient;
    private readonly PythonInferenceOptions _options;

    public PythonInferenceServiceHealthProbe(
        HttpClient httpClient,
        IOptions<PythonInferenceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                _options.HealthPath,
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
