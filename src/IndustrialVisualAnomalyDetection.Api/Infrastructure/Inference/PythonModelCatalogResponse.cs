namespace IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;

internal sealed record PythonModelCatalogResponse(
    string DefaultModelId,
    IReadOnlyList<PythonModelResponse>? Models);
