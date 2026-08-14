namespace IndustrialVisualAnomalyDetection.Api.Validation.Images;

public enum ImageUploadValidationFailure
{
    None,
    MissingFile,
    EmptyFile,
    FileTooLarge,
    UnsupportedContentType,
    InvalidFileSignature
}
