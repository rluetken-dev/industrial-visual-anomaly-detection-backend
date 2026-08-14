namespace IndustrialVisualAnomalyDetection.Api.Application.Analysis;

public sealed class InferenceUnavailableException : Exception
{
    public InferenceUnavailableException(string message)
        : base(ValidateMessage(message))
    {
    }

    public InferenceUnavailableException(
        string message,
        Exception innerException)
        : base(
            ValidateMessage(message),
            innerException
                ?? throw new ArgumentNullException(nameof(innerException)))
    {
    }

    private static string ValidateMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return message;
    }
}
