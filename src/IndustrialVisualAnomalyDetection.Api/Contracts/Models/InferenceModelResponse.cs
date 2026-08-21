namespace IndustrialVisualAnomalyDetection.Api.Contracts.Models;

public sealed record InferenceModelResponse(
    string Id,
    string DisplayName,
    string Category,
    int InputSize,
    bool IsDefault);
