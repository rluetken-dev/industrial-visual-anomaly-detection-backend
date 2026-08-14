namespace IndustrialVisualAnomalyDetection.Api.Application.Analysis;

public sealed record AnomalyAnalysisResult(
    string ModelId,
    string Category,
    double Score,
    double Threshold,
    bool IsAnomalous);
