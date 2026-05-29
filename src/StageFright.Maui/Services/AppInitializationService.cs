using StageFright.Core.Services;

namespace StageFright.Maui.Services;

/// <summary>
/// Implementation of IAppInitializationService that tracks application initialization state.
/// </summary>
public class AppInitializationService : IAppInitializationService
{
	private AppInitializationState _state = AppInitializationState.NotStarted;
	private string? _errorMessage;
	private readonly TaskCompletionSource<bool> _initializationCompletionSource = new();
	private readonly object _lockObject = new();

	/// <summary>
	/// Gets the current initialization state.
	/// </summary>
	public AppInitializationState State
	{
		get
		{
			lock (_lockObject)
			{
				return _state;
			}
		}
	}

	/// <summary>
	/// Gets the initialization error message if initialization failed.
	/// </summary>
	public string? ErrorMessage
	{
		get
		{
			lock (_lockObject)
			{
				return _errorMessage;
			}
		}
	}

	/// <summary>
	/// Waits for initialization to complete.
	/// Returns immediately if already initialized or failed.
	/// Throws if initialization failed with a critical error.
	/// </summary>
	public async Task WaitForInitializationAsync()
	{
		await _initializationCompletionSource.Task;

		lock (_lockObject)
		{
			if (_state == AppInitializationState.Failed)
			{
				throw new InvalidOperationException($"Application initialization failed: {_errorMessage}");
			}
		}
	}

	/// <summary>
	/// Marks initialization as complete.
	/// </summary>
	public void MarkInitializationComplete()
	{
		lock (_lockObject)
		{
			_state = AppInitializationState.Complete;
			_errorMessage = null;
		}

		_initializationCompletionSource.TrySetResult(true);
	}

	/// <summary>
	/// Marks initialization as failed with an error message.
	/// </summary>
	public void MarkInitializationFailed(string errorMessage)
	{
		lock (_lockObject)
		{
			_state = AppInitializationState.Failed;
			_errorMessage = errorMessage;
		}

		_initializationCompletionSource.TrySetResult(false);
	}
}
