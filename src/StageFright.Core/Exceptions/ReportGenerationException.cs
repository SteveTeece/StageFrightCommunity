namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when report generation fails.
/// </summary>
public class ReportGenerationException : Exception
{
    public ReportGenerationException(string message) : base(message)
    {
    }

    public ReportGenerationException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }

    public ReportGenerationException()
    {
    }
}
