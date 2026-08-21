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
    private readonly ILogger<AnalysesController> _logger;

    public AnalysesController(
        IImageUploadValidator imageUploadValidator,
        IAnomalyAnalyzer anomalyAnalyzer,
        ILogger<AnalysesController> logger)
    {
        ArgumentNullException.ThrowIfNull(imageUploadValidator);
        ArgumentNullException.ThrowIfNull(anomalyAnalyzer);
        ArgumentNullException.ThrowIfNull(logger);

        _imageUploadValidator = imageUploadValidator;
        _anomalyAnalyzer = anomalyAnalyzer;
        _logger = logger;
    }

    [HttpPost]
    [EndpointName("AnalyzeImage")]
    [EndpointSummary("Analyze an industrial image")]
    [EndpointDescription(
        "Validates one uploaded PNG or JPEG image and returns its anomaly score, decision threshold, classification decision, model information, processing time, and trace identifier.")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<AnalysisResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AnalysisResponse>> Analyze(
        [FromForm] AnalysisRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IFormFile? image = request.Image;
        ImageUploadValidationFailure validationFailure = _imageUploadValidator.Validate(image);

        if (validationFailure != ImageUploadValidationFailure.None)
        {
            _logger.LogWarning(
                "Rejected image upload {TraceId} with validation failure {ValidationFailure}.",
                HttpContext.TraceIdentifier,
                validationFailure);
        }

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

            case ImageUploadValidationFailure.InvalidFileSignature:
                return CreateValidationProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid image",
                    "The uploaded file content does not match its declared image type.",
                    "invalid-image");
        }

        _logger.LogInformation(
            "Starting anomaly analysis {TraceId} for an image with content type {ContentType} and size {FileSizeBytes}.",
            HttpContext.TraceIdentifier,
            image!.ContentType,
            image.Length);

        Stopwatch stopwatch = Stopwatch.StartNew();

        using Stream imageStream = image!.OpenReadStream();

        AnomalyAnalysisResult result = await _anomalyAnalyzer.AnalyzeAsync(
            new ImageAnalysisInput(
                imageStream,
                image.ContentType,
                HttpContext.TraceIdentifier,
                string.IsNullOrWhiteSpace(request.ModelId)
                    ? null
                    : request.ModelId),
            cancellationToken);

        stopwatch.Stop();

        _logger.LogInformation(
            "Completed anomaly analysis {TraceId} with model {ModelId}, category {Category}, decision {Decision}, and duration {ProcessingTimeMs} ms.",
            HttpContext.TraceIdentifier,
            result.ModelId,
            result.Category,
            result.IsAnomalous ? "anomalous" : "normal",
            stopwatch.ElapsedMilliseconds);

        AnalysisResponse response = new(
            new AnalysisModelResponse(result.ModelId, result.Category),
            result.Score,
            result.Threshold,
            result.IsAnomalous ? "anomalous" : "normal",
            stopwatch.ElapsedMilliseconds,
            HttpContext.TraceIdentifier,
            new AnalysisHeatmapResponse(
                result.Heatmap.ContentType,
                result.Heatmap.Width,
                result.Heatmap.Height,
                result.Heatmap.DataBase64));

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
