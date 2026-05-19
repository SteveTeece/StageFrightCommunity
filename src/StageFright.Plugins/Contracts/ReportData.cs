namespace StageFright.Plugins.Contracts;

using System;
using System.Collections.Generic;

/// <summary>Report data structure.</summary>
public class ReportData
{
	public string ReportTitle { get; set; } = string.Empty;
	public string[] ColumnHeaders { get; set; } = Array.Empty<string>();
	public string[][] Rows { get; set; } = Array.Empty<string[]>();
	public Dictionary<string, string>? Summaries { get; set; }
	public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
