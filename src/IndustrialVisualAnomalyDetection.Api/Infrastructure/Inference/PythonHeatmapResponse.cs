namespace IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;

internal sealed record PythonHeatmapResponse(
    string ContentType,
    int Width,
    int Height,
    string DataBase64);
