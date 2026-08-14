namespace IndustrialVisualAnomalyDetection.Api.Application.Analysis;

public sealed class InferenceUnavailableException : Exception
{
    public InferenceUnavailableException(string message)
        : base(message)
    {
    }

    public InferenceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
