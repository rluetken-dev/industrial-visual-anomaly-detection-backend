using IndustrialVisualAnomalyDetection.Api.Application.Analysis;

namespace IndustrialVisualAnomalyDetection.Api.Infrastructure.Inference;

public sealed class UnavailableAnomalyAnalyzer : IAnomalyAnalyzer
{
    public Task<AnomalyAnalysisResult> AnalyzeAsync(ImageAnalysisInput input, CancellationToken cancellationToken)
    {
        throw new InferenceUnavailableException("No anomaly inference adapter is configured.");
    }
}
