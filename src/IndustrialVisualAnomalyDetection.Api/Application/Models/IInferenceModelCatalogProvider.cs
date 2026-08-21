namespace IndustrialVisualAnomalyDetection.Api.Application.Models;

public interface IInferenceModelCatalogProvider
{
    Task<InferenceModelCatalog> GetCatalogAsync(CancellationToken cancellationToken);
}
