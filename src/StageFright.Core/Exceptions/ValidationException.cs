namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when validation of business logic or data constraints fails.
/// </summary>
public class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }

    public ValidationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }

    public ValidationException()
    {
    }
}
