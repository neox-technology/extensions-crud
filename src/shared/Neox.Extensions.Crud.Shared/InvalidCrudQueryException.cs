namespace Neox.Extensions.Crud.Shared;

public sealed class InvalidCrudQueryException : Exception
{
    public InvalidCrudQueryException(string message)
        : base(message)
    {
    }

    public InvalidCrudQueryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
