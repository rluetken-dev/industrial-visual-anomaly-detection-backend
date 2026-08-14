namespace IndustrialVisualAnomalyDetection.Api.Application.Analysis;

public sealed record ImageAnalysisInput
{
    public ImageAnalysisInput(
        Stream content,
        string contentType,
        string? traceId = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        if (!content.CanRead)
        {
            throw new ArgumentException(
                "The image content stream must be readable.",
                nameof(content));
        }

        if (traceId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(traceId);
        }

        Content = content;
        ContentType = contentType;
        TraceId = traceId;
    }

    public Stream Content { get; }
    public string ContentType { get; }
    public string? TraceId { get; }
}
