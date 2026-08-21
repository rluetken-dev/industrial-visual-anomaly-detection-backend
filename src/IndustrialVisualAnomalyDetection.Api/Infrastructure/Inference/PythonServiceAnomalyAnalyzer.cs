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
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Value);

        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<AnomalyAnalysisResult> AnalyzeAsync(
        ImageAnalysisInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        using StreamContent imageContent = new(input.Content);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(input.ContentType);

        using MultipartFormDataContent form = new();
        form.Add(imageContent, "image", "image");

        if (input.ModelId is not null)
        {
            form.Add(new StringContent(input.ModelId), "modelId");
        }

        try
        {
            using HttpRequestMessage request = new(
                HttpMethod.Post,
                _options.PredictionPath)
            {
                Content = form
            };

            if (!string.IsNullOrWhiteSpace(input.TraceId))
            {
                request.Headers.TryAddWithoutValidation(
                    "X-Correlation-ID",
                    input.TraceId);
            }

            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                throw new InvalidImageContentException(
                    "The Python inference service rejected the uploaded image.");
            }

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

            PythonHeatmapResponse heatmap = inferenceResponse.Heatmap!;

            return new AnomalyAnalysisResult(
                inferenceResponse.ModelId,
                inferenceResponse.Category,
                inferenceResponse.Score,
                inferenceResponse.Threshold,
                inferenceResponse.IsAnomalous,
                new AnomalyHeatmap(
                    heatmap.ContentType,
                    heatmap.Width,
                    heatmap.Height,
                    heatmap.DataBase64));
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

        if (!IsValid(response.Heatmap))
        {
            return false;
        }

        bool expectedDecision = response.Score > response.Threshold;
        return response.IsAnomalous == expectedDecision;
    }

    private static bool IsValid(PythonHeatmapResponse? heatmap)
    {
        if (heatmap is null
            || !string.Equals(heatmap.ContentType, "image/png", StringComparison.OrdinalIgnoreCase)
            || heatmap.Width <= 0
            || heatmap.Height <= 0
            || string.IsNullOrWhiteSpace(heatmap.DataBase64))
        {
            return false;
        }

        byte[] buffer = new byte[heatmap.DataBase64.Length];
        return Convert.TryFromBase64String(heatmap.DataBase64, buffer, out _);
    }
}
