namespace IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;

internal sealed record PythonInferenceResponse(
    string ModelId,
    string Category,
    double Score,
    double Threshold,
    bool IsAnomalous);
