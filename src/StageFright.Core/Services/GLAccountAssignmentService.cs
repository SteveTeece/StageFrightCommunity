namespace StageFright.Core.Services;

using System;

/// <summary>Service for GL account assignment.</summary>
public class GLAccountAssignmentService
{
	private static readonly object _lockObject = new object();

	/// <summary>Assigns next available GL account number based on category type.</summary>
	public string AssignGLAccount(string categoryType)
	{
		lock (_lockObject)
		{
			return categoryType switch
			{
				"Income" => $"10{DateTime.Now.Ticks % 100:D2}",
				"Expense" => $"20{DateTime.Now.Ticks % 100:D2}",
				_ => throw new Exceptions.ValidationException("Invalid category type.")
			};
		}
	}
}
