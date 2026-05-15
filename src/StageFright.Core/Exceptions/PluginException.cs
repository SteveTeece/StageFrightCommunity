namespace StageFright.Core.Exceptions;

/// <summary>
/// Thrown when plugin loading, discovery, or execution fails.
/// </summary>
public class PluginException : Exception
{
    public PluginException(string message) : base(message)
    {
    }

    public PluginException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }

    public PluginException()
    {
    }
}
