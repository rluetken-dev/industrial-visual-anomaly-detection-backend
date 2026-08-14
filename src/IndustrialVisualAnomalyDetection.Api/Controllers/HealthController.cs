using IndustrialVisualAnomalyDetection.Api.Contracts.Health;
using Microsoft.AspNetCore.Mvc;

namespace IndustrialVisualAnomalyDetection.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("live")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetLiveness()
    {
        return Ok(new HealthResponse("healthy"));
    }

    [HttpGet("ready")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetReadiness()
    {
        return Ok(new HealthResponse("ready"));
    }
}