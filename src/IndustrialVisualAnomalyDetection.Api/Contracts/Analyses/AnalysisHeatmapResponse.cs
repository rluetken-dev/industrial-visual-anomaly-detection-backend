namespace IndustrialVisualAnomalyDetection.Api.Contracts.Analyses;

public sealed record AnalysisHeatmapResponse(
    string ContentType,
    int Width,
    int Height,
    string DataBase64);
