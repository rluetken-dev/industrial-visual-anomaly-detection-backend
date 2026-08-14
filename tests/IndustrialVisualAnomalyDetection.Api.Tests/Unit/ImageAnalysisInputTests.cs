using IndustrialVisualAnomalyDetection.Api.Application.Analysis;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Unit;

public sealed class ImageAnalysisInputTests
{
    [Fact]
    public void NullContentIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ImageAnalysisInput(null!, "image/png"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingContentTypeIsRejected(string contentType)
    {
        using MemoryStream content = new();

        Assert.Throws<ArgumentException>(() =>
            new ImageAnalysisInput(content, contentType));
    }

    [Fact]
    public void UnreadableContentIsRejected()
    {
        using MemoryStream content = new();
        content.Dispose();

        Assert.Throws<ArgumentException>(() =>
            new ImageAnalysisInput(content, "image/png"));
    }

    [Fact]
    public void EmptyTraceIdIsRejected()
    {
        using MemoryStream content = new();

        Assert.Throws<ArgumentException>(() =>
            new ImageAnalysisInput(content, "image/png", " "));
    }

    [Fact]
    public void ValidInputPreservesValues()
    {
        using MemoryStream content = new();

        ImageAnalysisInput input = new(
            content,
            "image/png",
            "trace-123");

        Assert.Same(content, input.Content);
        Assert.Equal("image/png", input.ContentType);
        Assert.Equal("trace-123", input.TraceId);
    }
}
