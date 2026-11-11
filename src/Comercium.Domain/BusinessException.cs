namespace Comercium.Domain;

public sealed class BusinessException(string message) : Exception(message);
