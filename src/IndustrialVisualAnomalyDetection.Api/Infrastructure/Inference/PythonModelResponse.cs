namespace IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;

internal sealed record PythonModelResponse(
    string Id,
    string DisplayName,
    string Category,
    int InputSize,
    bool IsDefault);
