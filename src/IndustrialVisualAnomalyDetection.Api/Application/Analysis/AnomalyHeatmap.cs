namespace IndustrialVisualAnomalyDetection.Api.Application.Analysis;

public sealed record AnomalyHeatmap
{
    public AnomalyHeatmap(string contentType, int width, int height, string dataBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataBase64);

        if (!string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The anomaly heatmap must use image/png.", nameof(contentType));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "The heatmap width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "The heatmap height must be greater than zero.");
        }

        try
        {
            Convert.FromBase64String(dataBase64);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("The heatmap data must be valid Base64.", nameof(dataBase64), exception);
        }

        ContentType = contentType;
        Width = width;
        Height = height;
        DataBase64 = dataBase64;
    }

    public string ContentType { get; }
    public int Width { get; }
    public int Height { get; }
    public string DataBase64 { get; }
}
