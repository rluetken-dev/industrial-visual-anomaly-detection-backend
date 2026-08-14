namespace IndustrialVisualAnomalyDetection.Api.Contracts.Analyses;

public sealed record AnalysisResponse(
    AnalysisModelResponse Model,
    double Score,
    double Threshold,
    string Decision,
    long ProcessingTimeMs,
    string TraceId);
