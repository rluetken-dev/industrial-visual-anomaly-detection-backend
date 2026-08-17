using IndustrialVisualAnomalyDetection.Api.Application.Analysis;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Unit;

public sealed class AnomalyHeatmapTests
{
    [Fact]
    public void ValidHeatmapPreservesValues()
    {
        string dataBase64 = Convert.ToBase64String([1, 2, 3]);

        AnomalyHeatmap heatmap = new(
            "image/png",
            320,
            320,
            dataBase64);

        Assert.Equal("image/png", heatmap.ContentType);
        Assert.Equal(320, heatmap.Width);
        Assert.Equal(320, heatmap.Height);
        Assert.Equal(dataBase64, heatmap.DataBase64);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("image/jpeg")]
    public void InvalidContentTypeIsRejected(string contentType)
    {
        Assert.Throws<ArgumentException>(() =>
            new AnomalyHeatmap(
                contentType,
                320,
                320,
                Convert.ToBase64String([1])));
    }

    [Theory]
    [InlineData(0, 320)]
    [InlineData(-1, 320)]
    [InlineData(320, 0)]
    [InlineData(320, -1)]
    public void InvalidDimensionsAreRejected(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AnomalyHeatmap(
                "image/png",
                width,
                height,
                Convert.ToBase64String([1])));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-base64")]
    public void InvalidBase64DataIsRejected(string dataBase64)
    {
        Assert.Throws<ArgumentException>(() =>
            new AnomalyHeatmap(
                "image/png",
                320,
                320,
                dataBase64));
    }
}
