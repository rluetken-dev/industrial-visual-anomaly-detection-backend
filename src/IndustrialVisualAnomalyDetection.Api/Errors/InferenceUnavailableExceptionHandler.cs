using IndustrialVisualAnomalyDetection.Api.Application.Analysis;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace IndustrialVisualAnomalyDetection.Api.Errors;

public sealed class InferenceUnavailableExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public InferenceUnavailableExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not InferenceUnavailableException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Type = "https://github.com/rluetken-dev/industrial-visual-anomaly-detection-backend/problems/inference-unavailable",
                Title = "Inference unavailable",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = "The anomaly inference service is currently unavailable.",
                Instance = httpContext.Request.Path
            },
            Exception = exception
        });
    }
}
