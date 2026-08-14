namespace IndustrialVisualAnomalyDetection.Api.Application.Analysis;

public interface IAnomalyAnalyzer
{
    Task<AnomalyAnalysisResult> AnalyzeAsync(ImageAnalysisInput input, CancellationToken cancellationToken);
}
