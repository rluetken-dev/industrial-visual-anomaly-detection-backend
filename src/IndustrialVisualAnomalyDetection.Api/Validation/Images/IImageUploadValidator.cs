namespace IndustrialVisualAnomalyDetection.Api.Validation.Images;

public interface IImageUploadValidator
{
    ImageUploadValidationFailure Validate(IFormFile? image);
}
