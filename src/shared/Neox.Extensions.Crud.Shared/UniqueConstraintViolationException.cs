namespace Neox.Extensions.Crud.Shared;

public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException(string constraintName, Exception innerException)
        : base($"Unique constraint '{constraintName}' was violated.", innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(constraintName);

        ConstraintName = constraintName;
    }

    public string ConstraintName { get; }
}
