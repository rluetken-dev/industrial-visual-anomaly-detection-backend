namespace IndustrialVisualAnomalyDetection.Api.Options;

public sealed class ImageUploadOptions
{
    public const string SectionName = "ImageUpload";
    public const long DefaultMaxFileSizeBytes = 10 * 1024 * 1024;
    public long MaxFileSizeBytes { get; init; } = DefaultMaxFileSizeBytes;
    public string[] AllowedContentTypes { get; init; } = [];
}
