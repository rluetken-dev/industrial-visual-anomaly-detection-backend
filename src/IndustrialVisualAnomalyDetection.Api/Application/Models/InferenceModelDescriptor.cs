namespace IndustrialVisualAnomalyDetection.Api.Application.Models;

public sealed record InferenceModelDescriptor
{
    public InferenceModelDescriptor(
        string id,
        string displayName,
        string category,
        int inputSize,
        bool isDefault)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        if (inputSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inputSize),
                "The model input size must be greater than zero.");
        }

        Id = id;
        DisplayName = displayName;
        Category = category;
        InputSize = inputSize;
        IsDefault = isDefault;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public int InputSize { get; }
    public bool IsDefault { get; }
}
