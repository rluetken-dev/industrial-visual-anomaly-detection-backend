using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IndustrialVisualAnomalyDetection.Api.Application.Analysis;
using IndustrialVisualAnomalyDetection.Api.Options;
using Microsoft.Extensions.Options;

namespace IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;

public sealed class PythonServiceAnomalyAnalyzer : IAnomalyAnalyzer
{
    private readonly HttpClient _httpClient;
    private readonly PythonInferenceOptions _options;

    public PythonServiceAnomalyAnalyzer(
        HttpClient httpClient,
        IOptions<PythonInferenceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<AnomalyAnalysisResult> AnalyzeAsync(
        ImageAnalysisInput input,
        CancellationToken cancellationToken)
    {
        using StreamContent imageContent = new(input.Content);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(input.ContentType);

        using MultipartFormDataContent form = new();
        form.Add(imageContent, "image", "image");

        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsync(
                _options.PredictionPath,
                form,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InferenceUnavailableException(
                    $"The Python inference service returned HTTP {(int)response.StatusCode}.");
            }

            PythonInferenceResponse? inferenceResponse =
                await response.Content.ReadFromJsonAsync<PythonInferenceResponse>(
                    cancellationToken);

            if (inferenceResponse is null)
            {
                throw new InferenceUnavailableException(
                    "The Python inference service returned an empty response.");
            }

            if (!IsValid(inferenceResponse))
            {
                throw new InferenceUnavailableException(
                    "The Python inference service returned an invalid response.");
            }

            return new AnomalyAnalysisResult(
                inferenceResponse.ModelId,
                inferenceResponse.Category,
                inferenceResponse.Score,
                inferenceResponse.Threshold,
                inferenceResponse.IsAnomalous);
        }
        catch (HttpRequestException exception)
        {
            throw new InferenceUnavailableException(
                "The Python inference service could not be reached.",
                exception);
        }
        catch (JsonException exception)
        {
            throw new InferenceUnavailableException(
                "The Python inference service returned an invalid response.",
                exception);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new InferenceUnavailableException(
                "The Python inference request timed out.",
                exception);
        }
    }

    private static bool IsValid(PythonInferenceResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.ModelId)
            || string.IsNullOrWhiteSpace(response.Category))
        {
            return false;
        }

        if (!double.IsFinite(response.Score)
            || !double.IsFinite(response.Threshold)
            || response.Score < 0
            || response.Threshold < 0)
        {
            return false;
        }

        bool expectedDecision = response.Score > response.Threshold;

        return response.IsAnomalous == expectedDecision;
    }
}
