using System.Net.Http.Json;
using System.Text.Json;
using IndustrialVisualAnomalyDetection.Api.Application.Analysis;
using IndustrialVisualAnomalyDetection.Api.Application.Models;
using IndustrialVisualAnomalyDetection.Api.Options;
using Microsoft.Extensions.Options;

namespace IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;

public sealed class PythonInferenceModelCatalogProvider
    : IInferenceModelCatalogProvider
{
    private readonly HttpClient _httpClient;
    private readonly PythonInferenceOptions _options;

    public PythonInferenceModelCatalogProvider(
        HttpClient httpClient,
        IOptions<PythonInferenceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Value);

        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<InferenceModelCatalog> GetCatalogAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(
                _options.ModelCatalogPath,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InferenceUnavailableException(
                    $"The Python inference service returned HTTP {(int)response.StatusCode} while loading the model catalog.");
            }

            PythonModelCatalogResponse? catalogResponse =
                await response.Content.ReadFromJsonAsync<PythonModelCatalogResponse>(
                    cancellationToken);

            if (catalogResponse?.Models is null)
            {
                throw InvalidResponse();
            }

            try
            {
                return new InferenceModelCatalog(
                    catalogResponse.DefaultModelId,
                    catalogResponse.Models.Select(model =>
                        new InferenceModelDescriptor(
                            model.Id,
                            model.DisplayName,
                            model.Category,
                            model.InputSize,
                            model.IsDefault)));
            }
            catch (ArgumentException exception)
            {
                throw InvalidResponse(exception);
            }
        }
        catch (HttpRequestException exception)
        {
            throw new InferenceUnavailableException(
                "The Python inference service could not be reached while loading the model catalog.",
                exception);
        }
        catch (JsonException exception)
        {
            throw InvalidResponse(exception);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new InferenceUnavailableException(
                "The Python inference model catalog request timed out.",
                exception);
        }
    }

    private static InferenceUnavailableException InvalidResponse()
    {
        return new InferenceUnavailableException(
            "The Python inference service returned an invalid model catalog.");
    }

    private static InferenceUnavailableException InvalidResponse(
        Exception innerException)
    {
        return new InferenceUnavailableException(
            "The Python inference service returned an invalid model catalog.",
            innerException);
    }
}
