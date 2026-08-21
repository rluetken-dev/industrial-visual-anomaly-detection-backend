namespace IndustrialVisualAnomalyDetection.Api.Application.Models;

public sealed record InferenceModelCatalog
{
    public InferenceModelCatalog(
        string defaultModelId,
        IEnumerable<InferenceModelDescriptor> models)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultModelId);
        ArgumentNullException.ThrowIfNull(models);

        InferenceModelDescriptor[] modelArray = models.ToArray();

        if (modelArray.Length == 0)
        {
            throw new ArgumentException("The model catalog must contain at least one model.", nameof(models));
        }

        if (modelArray.Select(model => model.Id).Distinct().Count()
            != modelArray.Length)
        {
            throw new ArgumentException("The model catalog must contain unique model IDs.", nameof(models));
        }

        InferenceModelDescriptor[] defaultModels = modelArray
            .Where(model => model.IsDefault)
            .ToArray();

        if (defaultModels.Length != 1
            || defaultModels[0].Id != defaultModelId)
        {
            throw new ArgumentException("The model catalog must contain exactly one matching default model.", nameof(models));
        }

        DefaultModelId = defaultModelId;
        Models = modelArray;
    }

    public string DefaultModelId { get; }
    public IReadOnlyList<InferenceModelDescriptor> Models { get; }
}
