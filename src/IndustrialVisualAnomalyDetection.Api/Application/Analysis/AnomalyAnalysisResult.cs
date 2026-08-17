namespace IndustrialVisualAnomalyDetection.Api.Application.Analysis;

public sealed record AnomalyAnalysisResult
{
    public AnomalyAnalysisResult(
        string modelId,
        string category,
        double score,
        double threshold,
        bool isAnomalous,
        AnomalyHeatmap heatmap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(heatmap);

        if (!double.IsFinite(score) || score < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "The anomaly score must be a finite, non-negative value.");
        }

        if (!double.IsFinite(threshold) || threshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "The decision threshold must be a finite, non-negative value.");
        }

        if (isAnomalous != (score > threshold))
        {
            throw new ArgumentException("The anomaly decision must match the score and threshold.", nameof(isAnomalous));
        }

        ModelId = modelId;
        Category = category;
        Score = score;
        Threshold = threshold;
        IsAnomalous = isAnomalous;
        Heatmap = heatmap;
    }

    public string ModelId { get; }
    public string Category { get; }
    public double Score { get; }
    public double Threshold { get; }
    public bool IsAnomalous { get; }
    public AnomalyHeatmap Heatmap { get; }
}
