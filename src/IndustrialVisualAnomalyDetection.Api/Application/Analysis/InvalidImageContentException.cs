namespace IndustrialVisualAnomalyDetection.Api.Application.Analysis;

public sealed class InvalidImageContentException : Exception
{
    public InvalidImageContentException(string message)
        : base(ValidateMessage(message))
    {
    }

    private static string ValidateMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return message;
    }
}
