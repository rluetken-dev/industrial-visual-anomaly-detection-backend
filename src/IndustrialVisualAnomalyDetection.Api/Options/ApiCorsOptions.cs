namespace IndustrialVisualAnomalyDetection.Api.Options;

public sealed class ApiCorsOptions
{
    public const string SectionName = "Cors";
    public const string PolicyName = "ConfiguredClients";

    public string[] AllowedOrigins { get; init; } = [];
}
