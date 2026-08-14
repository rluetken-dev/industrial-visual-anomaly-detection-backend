using System.Diagnostics;
using IndustrialVisualAnomalyDetection.Api.Application.Analysis;
using IndustrialVisualAnomalyDetection.Api.Contracts.Analyses;
using IndustrialVisualAnomalyDetection.Api.Validation.Images;
using Microsoft.AspNetCore.Mvc;

namespace IndustrialVisualAnomalyDetection.Api.Controllers;

[ApiController]
[Route("api/v1/analyses")]
public sealed class AnalysesController : ControllerBase
{
    private readonly IImageUploadValidator _imageUploadValidator;
    private readonly IAnomalyAnalyzer _anomalyAnalyzer;

    public AnalysesController(
        IImageUploadValidator imageUploadValidator,
        IAnomalyAnalyzer anomalyAnalyzer)
    {
        _imageUploadValidator = imageUploadValidator;
        _anomalyAnalyzer = anomalyAnalyzer;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<AnalysisResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AnalysisResponse>> Analyze(
        [FromForm] IFormFile? image,
        CancellationToken cancellationToken)
    {
        ImageUploadValidationFailure validationFailure = _imageUploadValidator.Validate(image);

        switch (validationFailure)
        {
            case ImageUploadValidationFailure.MissingFile:
                return CreateValidationProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid image",
                    "An image file is required.",
                    "invalid-image");

            case ImageUploadValidationFailure.EmptyFile:
                return CreateValidationProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid image",
                    "The uploaded image file must not be empty.",
                    "invalid-image");

            case ImageUploadValidationFailure.FileTooLarge:
                return CreateValidationProblem(
                    StatusCodes.Status413PayloadTooLarge,
                    "Image too large",
                    "The uploaded image exceeds the configured size limit.",
                    "image-too-large");

            case ImageUploadValidationFailure.UnsupportedContentType:
                return CreateValidationProblem(
                    StatusCodes.Status415UnsupportedMediaType,
                    "Unsupported image type",
                    "The uploaded file does not use a supported image content type.",
                    "unsupported-image-type");
        }

        Stopwatch stopwatch = Stopwatch.StartNew();

        using Stream imageStream = image!.OpenReadStream();
        AnomalyAnalysisResult result = await _anomalyAnalyzer.AnalyzeAsync(
            new ImageAnalysisInput(imageStream, image.ContentType),
            cancellationToken);

        stopwatch.Stop();

        AnalysisResponse response = new(
            new AnalysisModelResponse(result.ModelId, result.Category),
            result.Score,
            result.Threshold,
            result.IsAnomalous ? "anomalous" : "normal",
            stopwatch.ElapsedMilliseconds,
            HttpContext.TraceIdentifier);

        return Ok(response);
    }

    private ObjectResult CreateValidationProblem(
        int statusCode,
        string title,
        string detail,
        string problemCode)
    {
        return Problem(
            type: $"https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/{problemCode}",
            title: title,
            statusCode: statusCode,
            detail: detail,
            instance: HttpContext.Request.Path);
    }
}
