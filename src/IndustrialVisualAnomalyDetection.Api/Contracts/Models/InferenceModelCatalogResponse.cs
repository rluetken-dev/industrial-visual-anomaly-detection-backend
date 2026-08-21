namespace IndustrialVisualAnomalyDetection.Api.Contracts.Models;

public sealed record InferenceModelCatalogResponse(
    string DefaultModelId,
    IReadOnlyList<InferenceModelResponse> Models);
