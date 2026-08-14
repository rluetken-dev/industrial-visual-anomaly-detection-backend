using Microsoft.AspNetCore.Mvc;

namespace IndustrialVisualAnomalyDetection.Api.Contracts.Analyses;

public sealed class AnalysisRequest
{
    [FromForm(Name = "image")]
    public IFormFile? Image { get; init; }
}
