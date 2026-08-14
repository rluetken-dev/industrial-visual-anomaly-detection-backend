namespace IndustrialVisualAnomalyDetection.Api.Application.Health;

public interface IInferenceServiceHealthProbe
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}
