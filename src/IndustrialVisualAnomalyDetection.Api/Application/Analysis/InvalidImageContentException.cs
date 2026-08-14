namespace IndustrialVisualAnomalyDetection.Api.Application.Analysis;

public sealed class InvalidImageContentException : Exception
{
    public InvalidImageContentException(string message)
        : base(message)
    {
    }
}
