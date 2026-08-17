using IndustrialVisualAnomalyDetection.Api.Application.Analysis;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Unit;

public sealed class AnomalyAnalysisResultTests
{
    private static readonly AnomalyHeatmap ValidHeatmap = new(
        "image/png",
        320,
        320,
        Convert.ToBase64String([1, 2, 3]));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingModelIdIsRejected(string modelId)
    {
        Assert.Throws<ArgumentException>(() =>
            new AnomalyAnalysisResult(
                modelId,
                "capsule",
                2.0,
                1.0,
                true,
                ValidHeatmap));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingCategoryIsRejected(string category)
    {
        Assert.Throws<ArgumentException>(() =>
            new AnomalyAnalysisResult(
                "model",
                category,
                2.0,
                1.0,
                true,
                ValidHeatmap));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidScoreIsRejected(double score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AnomalyAnalysisResult(
                "model",
                "capsule",
                score,
                1.0,
                false,
                ValidHeatmap));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidThresholdIsRejected(double threshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AnomalyAnalysisResult(
                "model",
                "capsule",
                1.0,
                threshold,
                false,
                ValidHeatmap));
    }

    [Fact]
    public void InconsistentDecisionIsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new AnomalyAnalysisResult(
                "model",
                "capsule",
                2.0,
                1.0,
                false,
                ValidHeatmap));
    }

    [Fact]
    public void MissingHeatmapIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AnomalyAnalysisResult(
                "model",
                "capsule",
                2.0,
                1.0,
                true,
                null!));
    }

    [Fact]
    public void ValidResultPreservesValues()
    {
        AnomalyAnalysisResult result = new(
            "model",
            "capsule",
            2.0,
            1.0,
            true,
            ValidHeatmap);

        Assert.Equal("model", result.ModelId);
        Assert.Equal("capsule", result.Category);
        Assert.Equal(2.0, result.Score);
        Assert.Equal(1.0, result.Threshold);
        Assert.True(result.IsAnomalous);
        Assert.Same(ValidHeatmap, result.Heatmap);
    }
}
