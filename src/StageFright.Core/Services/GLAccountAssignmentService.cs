namespace StageFright.Core.Services;

/// <summary>
/// Service for GL account assignment with sequential numbering.
/// GL account structure: Asset GL#01xx (0100/0101 fixed), Income GL#10xx, Expense GL#20xx, BadDebtExpense GL#9900 fixed.
/// </summary>
public class GLAccountAssignmentService
{
	private static int _incomeCounter = 1000; // Starting at 1000 for Income GL#10xx
	private static int _expenseCounter = 2000; // Starting at 2000 for Expense GL#20xx
	private static readonly object _lockObject = new object();

	/// <summary>
	/// Assigns next available GL account number based on category type.
	/// </summary>
	public string AssignGLAccount(string categoryType)
	{
		lock (_lockObject)
		{
			return categoryType switch
			{
				"Income" => (_incomeCounter++).ToString(),
				"Expense" => (_expenseCounter++).ToString(),
				_ => throw new Exceptions.ValidationException($"Invalid category type: {categoryType}")
			};
		}
	}

	/// <summary>
	/// Gets the fixed GL account for bad debt expense.
	/// </summary>
	public static string GetBadDebtExpenseAccount() => "9900";

	/// <summary>
	/// Gets the fixed GL account for asset (Accounts Receivable).
	/// </summary>
	public static string GetAssetAccount() => "0100";

	/// <summary>
	/// Gets the fixed GL account for undeposited funds.
	/// </summary>
	public static string GetUndepositedFundsAccount() => "0101";

	/// <summary>
	/// Resets counters (for testing purposes).
	/// </summary>
	public static void ResetCounters()
	{
		_incomeCounter = 1000;
		_expenseCounter = 2000;
	}
}
