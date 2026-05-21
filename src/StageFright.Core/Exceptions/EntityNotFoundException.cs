namespace StageFright.Core.Exceptions;

/// <summary>Exception thrown when a requested entity is not found in the database.</summary>
public class EntityNotFoundException : Exception
{
	/// <summary>Initializes a new instance of the EntityNotFoundException class.</summary>
	public EntityNotFoundException()
	{
	}

	/// <summary>Initializes a new instance of the EntityNotFoundException class with a specified error message.</summary>
	/// <param name="message">The message that describes the error.</param>
	public EntityNotFoundException(string message) : base(message)
	{
	}

	/// <summary>Initializes a new instance of the EntityNotFoundException class with a specified error message and inner exception.</summary>
	/// <param name="message">The message that describes the error.</param>
	/// <param name="innerException">The exception that is the cause of the current exception.</param>
	public EntityNotFoundException(string message, Exception innerException) : base(message, innerException)
	{
	}
}
