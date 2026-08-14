using IndustrialVisualAnomalyDetection.Api.Application.Health;
using IndustrialVisualAnomalyDetection.Api.Contracts.Health;
using Microsoft.AspNetCore.Mvc;

namespace IndustrialVisualAnomalyDetection.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    private readonly IInferenceServiceHealthProbe _inferenceServiceHealthProbe;

    public HealthController(IInferenceServiceHealthProbe inferenceServiceHealthProbe)
    {
        _inferenceServiceHealthProbe = inferenceServiceHealthProbe;
    }

    [HttpGet("live")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetLiveness()
    {
        return Ok(new HealthResponse("healthy"));
    }

    [HttpGet("ready")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> GetReadiness(CancellationToken cancellationToken)
    {
        bool isReady = await _inferenceServiceHealthProbe.IsReadyAsync(cancellationToken);

        if (!isReady)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new HealthResponse("not_ready"));
        }

        return Ok(new HealthResponse("ready"));
    }
}
