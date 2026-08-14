using IndustrialVisualAnomalyDetection.Api.Options;
using Microsoft.Extensions.Options;

namespace IndustrialVisualAnomalyDetection.Api.Validation.Images;

public sealed class ImageUploadValidator : IImageUploadValidator
{
    private readonly ImageUploadOptions _options;

    public ImageUploadValidator(IOptions<ImageUploadOptions> options)
    {
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

        return ImageUploadValidationFailure.None;
    }
}
