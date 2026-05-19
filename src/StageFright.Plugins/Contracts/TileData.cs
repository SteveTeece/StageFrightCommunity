namespace StageFright.Plugins.Contracts;

using System.Collections.Generic;

/// <summary>Data structure for dashboard tile content.</summary>
public class TileData
{
	public string Title { get; set; } = string.Empty;
	public string Content { get; set; } = string.Empty;
	public Dictionary<string, string> Metrics { get; set; } = new();
	public string? Color { get; set; }
	public bool IsError { get; set; }
	public string? ErrorMessage { get; set; }
}
