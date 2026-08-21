namespace IndustrialVisualAnomalyDetection.Api.Options;

public sealed class PythonInferenceOptions
{
    public const string SectionName = "PythonInference";

    public string BaseUrl { get; init; } = string.Empty;
    public string PredictionPath { get; init; } = "/api/v1/predictions";
    public string ModelCatalogPath { get; init; } = "/api/v1/models";
    public string HealthPath { get; init; } = "/health/live";
    public int TimeoutSeconds { get; init; } = 30;
}
