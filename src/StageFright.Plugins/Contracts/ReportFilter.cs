namespace StageFright.Plugins.Contracts;

using System;
using System.Collections.Generic;

/// <summary>Filter criteria for report generation.</summary>
public class ReportFilter
{
	public DateTime? DateFrom { get; set; }
	public DateTime? DateTo { get; set; }
	public string? CategoryFilter { get; set; }
	public string? MemberStatusFilter { get; set; }
	public Dictionary<string, object> CustomFilters { get; set; } = new();
}
