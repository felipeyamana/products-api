namespace Application.Products.Exceptions;

public sealed class DuplicateExternalProductIdException(string externalProductId)
    : InvalidOperationException($"A product with external id '{externalProductId}' already exists.")
{
    public string ExternalProductId { get; } = externalProductId;
}
