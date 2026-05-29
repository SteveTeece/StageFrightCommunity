namespace StageFright.Core.Services;

/// <summary>
/// Service for tracking application initialization state.
/// Ensures database migrations and other startup tasks complete before the UI is fully interactive.
/// </summary>
public interface IAppInitializationService
{
	/// <summary>
	/// Gets the current initialization state.
	/// </summary>
	AppInitializationState State { get; }

	/// <summary>
	/// Gets the initialization error message if initialization failed.
	/// </summary>
	string? ErrorMessage { get; }

	/// <summary>
	/// Waits for initialization to complete.
	/// Returns immediately if already initialized or failed.
	/// Throws if initialization failed with a critical error.
	/// </summary>
	Task WaitForInitializationAsync();

	/// <summary>
	/// Marks initialization as complete.
	/// </summary>
	void MarkInitializationComplete();

	/// <summary>
	/// Marks initialization as failed with an error message.
	/// </summary>
	void MarkInitializationFailed(string errorMessage);
}

/// <summary>
/// Represents the current state of application initialization.
/// </summary>
public enum AppInitializationState
{
	/// <summary>
	/// Initialization has not started yet.
	/// </summary>
	NotStarted,

	/// <summary>
	/// Initialization is currently in progress.
	/// </summary>
	InProgress,

	/// <summary>
	/// Initialization completed successfully.
	/// </summary>
	Complete,

	/// <summary>
	/// Initialization failed with an error.
	/// </summary>
	Failed
}
