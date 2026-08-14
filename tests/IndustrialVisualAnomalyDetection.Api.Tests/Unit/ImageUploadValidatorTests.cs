using IndustrialVisualAnomalyDetection.Api.Options;
using IndustrialVisualAnomalyDetection.Api.Validation.Images;
using Microsoft.AspNetCore.Http;

namespace IndustrialVisualAnomalyDetection.Api.Tests.Unit;

public sealed class ImageUploadValidatorTests
{
    [Fact]
    public void MissingFileIsRejected()
    {
        ImageUploadValidator validator = CreateValidator();

        ImageUploadValidationFailure result = validator.Validate(null);

        Assert.Equal(ImageUploadValidationFailure.MissingFile, result);
    }

    [Fact]
    public void EmptyFileIsRejected()
    {
        ImageUploadValidator validator = CreateValidator();
        FormFile image = CreateFile([], "image/png");

        ImageUploadValidationFailure result = validator.Validate(image);

        Assert.Equal(ImageUploadValidationFailure.EmptyFile, result);
    }

    [Fact]
    public void OversizedFileIsRejected()
    {
        ImageUploadValidator validator = CreateValidator(maxFileSizeBytes: 4);
        FormFile image = CreateFile([1, 2, 3, 4, 5], "image/png");

        ImageUploadValidationFailure result = validator.Validate(image);

        Assert.Equal(ImageUploadValidationFailure.FileTooLarge, result);
    }

    [Fact]
    public void UnsupportedContentTypeIsRejected()
    {
        ImageUploadValidator validator = CreateValidator();
        FormFile image = CreateFile([1], "image/gif");

        ImageUploadValidationFailure result = validator.Validate(image);

        Assert.Equal(ImageUploadValidationFailure.UnsupportedContentType, result);
    }

    [Fact]
    public void SupportedContentTypeIsAcceptedCaseInsensitively()
    {
        ImageUploadValidator validator = CreateValidator();
        FormFile image = CreateFile([1], "IMAGE/PNG");

        ImageUploadValidationFailure result = validator.Validate(image);

        Assert.Equal(ImageUploadValidationFailure.None, result);
    }

    private static ImageUploadValidator CreateValidator(long maxFileSizeBytes = 10)
    {
        ImageUploadOptions options = new()
        {
            MaxFileSizeBytes = maxFileSizeBytes,
            AllowedContentTypes = ["image/png", "image/jpeg"]
        };

        return new ImageUploadValidator(Microsoft.Extensions.Options.Options.Create(options));
    }

    private static FormFile CreateFile(byte[] content, string contentType)
    {
        MemoryStream stream = new(content);

        return new FormFile(stream, 0, stream.Length, "image", "image.bin")
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
