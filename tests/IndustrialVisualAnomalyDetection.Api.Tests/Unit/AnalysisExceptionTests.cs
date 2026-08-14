using IndustrialVisualAnomalyDetection.Api.Application.Analysis;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Unit;

public sealed class AnalysisExceptionTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void InvalidImageExceptionRejectsMissingMessage(string message)
    {
        Assert.Throws<ArgumentException>(() =>
            new InvalidImageContentException(message));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void InferenceExceptionRejectsMissingMessage(string message)
    {
        Assert.Throws<ArgumentException>(() =>
            new InferenceUnavailableException(message));
    }

    [Fact]
    public void InferenceExceptionRejectsNullInnerException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InferenceUnavailableException(
                "Inference failed.",
                null!));
    }

    [Fact]
    public void InferenceExceptionPreservesInnerException()
    {
        InvalidOperationException innerException = new(
            "Connection failed.");

        InferenceUnavailableException exception = new(
            "Inference failed.",
            innerException);

        Assert.Equal("Inference failed.", exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }
}
