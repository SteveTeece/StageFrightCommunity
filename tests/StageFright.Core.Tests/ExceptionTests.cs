using StageFright.Core.Exceptions;
using Xunit;

namespace StageFright.Core.Tests;

/// <summary>
/// Tests for custom exception hierarchy.
/// Verifies that all exception types can be created and thrown properly.
/// </summary>
public class ExceptionTests
{
	[Fact]
	public void ValidationException_CanBeCreatedWithMessage()
	{
		// Arrange
		var message = "Validation failed";

		// Act & Assert
		ValidationException caughtException = null;
		try
		{
			throw new ValidationException(message);
		}
		catch (ValidationException ex)
		{
			caughtException = ex;
		}
		
		Assert.NotNull(caughtException);
		Assert.Equal(message, caughtException.Message);
	}

	[Fact]
	public void DataAccessException_CanBeCreatedWithMessageAndInnerException()
	{
		// Arrange
		var message = "Data access failed";
		var innerEx = new InvalidOperationException("Inner error");

		// Act & Assert
		DataAccessException caughtException = null;
		try
		{
			throw new DataAccessException(message, innerEx);
		}
		catch (DataAccessException ex)
		{
			caughtException = ex;
		}
		
		Assert.NotNull(caughtException);
		Assert.Equal(message, caughtException.Message);
		Assert.Equal(innerEx, caughtException.InnerException);
	}

	[Fact]
	public void PluginException_CanBeCreatedWithoutMessage()
	{
		// Act & Assert
		PluginException caughtException = null;
		try
		{
			throw new PluginException();
		}
		catch (PluginException ex)
		{
			caughtException = ex;
		}
		
		Assert.NotNull(caughtException);
	}

	[Fact]
	public void ReportGenerationException_CanBeCreated()
	{
		// Arrange
		var message = "Report generation failed";

		// Act & Assert
		ReportGenerationException caughtException = null;
		try
		{
			throw new ReportGenerationException(message);
		}
		catch (ReportGenerationException ex)
		{
			caughtException = ex;
		}
		
		Assert.NotNull(caughtException);
		Assert.Equal(message, caughtException.Message);
	}
}
