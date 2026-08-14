using IndustrialVisualAnomalyDetection.Api.Options;
using Microsoft.Extensions.Options;

namespace IndustrialVisualAnomalyDetection.Api.Validation.Images;

public sealed class ImageUploadValidator : IImageUploadValidator
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    private readonly ImageUploadOptions _options;

    public ImageUploadValidator(IOptions<ImageUploadOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Value);

        _options = options.Value;
    }

    public ImageUploadValidationFailure Validate(IFormFile? image)
    {
        if (image is null)
        {
            return ImageUploadValidationFailure.MissingFile;
        }

        if (image.Length == 0)
        {
            return ImageUploadValidationFailure.EmptyFile;
        }

        if (image.Length > _options.MaxFileSizeBytes)
        {
            return ImageUploadValidationFailure.FileTooLarge;
        }

        if (!_options.AllowedContentTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return ImageUploadValidationFailure.UnsupportedContentType;
        }

        if (!HasValidSignature(image))
        {
            return ImageUploadValidationFailure.InvalidFileSignature;
        }

        return ImageUploadValidationFailure.None;
    }

    private static bool HasValidSignature(IFormFile image)
    {
        byte[] expectedSignature = image.ContentType.ToLowerInvariant() switch
        {
            "image/png" => PngSignature,
            "image/jpeg" => JpegSignature,
            _ => []
        };

        if (expectedSignature.Length == 0 || image.Length < expectedSignature.Length)
        {
            return false;
        }

        byte[] header = new byte[expectedSignature.Length];

        try
        {
            using Stream stream = image.OpenReadStream();
            int bytesRead = stream.Read(header, 0, header.Length);

            return bytesRead == expectedSignature.Length
                && header.AsSpan().SequenceEqual(expectedSignature);
        }
        catch (IOException)
        {
            return false;
        }
    }
}
