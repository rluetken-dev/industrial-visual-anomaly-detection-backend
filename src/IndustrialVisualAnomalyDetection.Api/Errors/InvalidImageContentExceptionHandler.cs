using IndustrialVisualAnomalyDetection.Api.Application.Analysis;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace IndustrialVisualAnomalyDetection.Api.Errors;

public sealed class InvalidImageContentExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<InvalidImageContentExceptionHandler> _logger;

    public InvalidImageContentExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<InvalidImageContentExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(problemDetailsService);
        ArgumentNullException.ThrowIfNull(logger);

        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not InvalidImageContentException)
        {
            return false;
        }

        _logger.LogWarning(
            exception,
            "Rejected an unreadable image for analysis {TraceId}.",
            httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Type = "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/invalid-image",
                Title = "Invalid image",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The uploaded file is not a readable image.",
                Instance = httpContext.Request.Path
            },
            Exception = exception
        });
    }
}
