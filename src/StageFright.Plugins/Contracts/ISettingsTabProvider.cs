namespace StageFright.Plugins.Contracts;

/// <summary>
/// Contract for settings tab providers.
/// Plugins implement this to contribute tabs to the Settings module.
/// </summary>
public interface ISettingsTabProvider
{
	/// <summary>Tab identifier (must be unique).</summary>
	string TabId { get; }

	/// <summary>Display name for the tab.</summary>
	string DisplayName { get; }

	/// <summary>Display order (Core tabs: 0-99, Plugin tabs: 100+).</summary>
	int DisplayOrder { get; }

	/// <summary>Gets the Blazor component path for this tab's UI.</summary>
	string ComponentPath { get; }
}
