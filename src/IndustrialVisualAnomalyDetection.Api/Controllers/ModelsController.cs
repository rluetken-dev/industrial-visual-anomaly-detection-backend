using IndustrialVisualAnomalyDetection.Api.Application.Models;
using IndustrialVisualAnomalyDetection.Api.Contracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace IndustrialVisualAnomalyDetection.Api.Controllers;

[ApiController]
[Route("api/v1/models")]
public sealed class ModelsController : ControllerBase
{
    private readonly IInferenceModelCatalogProvider _catalogProvider;

    public ModelsController(IInferenceModelCatalogProvider catalogProvider)
    {
        ArgumentNullException.ThrowIfNull(catalogProvider);
        _catalogProvider = catalogProvider;
    }

    [HttpGet]
    [EndpointName("GetInferenceModels")]
    [EndpointSummary("Get available inference models")]
    [EndpointDescription("Returns the inference models currently available for image analysis and identifies the default model.")]
    [ProducesResponseType<InferenceModelCatalogResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<InferenceModelCatalogResponse>> GetModels(
        CancellationToken cancellationToken)
    {
        InferenceModelCatalog catalog =
            await _catalogProvider.GetCatalogAsync(cancellationToken);

        InferenceModelCatalogResponse response = new(
            catalog.DefaultModelId,
            catalog.Models.Select(model => new InferenceModelResponse(
                model.Id,
                model.DisplayName,
                model.Category,
                model.InputSize,
                model.IsDefault))
            .ToArray());

        return Ok(response);
    }
}
