namespace StageFright.Reports.Providers;

using Microsoft.Extensions.Logging;
using StageFright.Data.Repositories;
using StageFright.Plugins.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Committee Report provider showing committee membership organized by year.
/// Displays member name, year, and position held.
/// </summary>
public class CommitteeReportProvider : IReportProvider
{
	private readonly ICommitteeMembershipRepository _committeeMembershipRepository;
	private readonly IMemberRepository _memberRepository;
	private readonly ILogger<CommitteeReportProvider> _logger;

	public string ModuleName => "Members";
	public string ReportId => "committee-report";
	public string ReportName => "Committee Report";
	public int DisplayOrder => 2;

	public CommitteeReportProvider(
		ICommitteeMembershipRepository committeeMembershipRepository,
		IMemberRepository memberRepository,
		ILogger<CommitteeReportProvider> logger)
	{
		_committeeMembershipRepository = committeeMembershipRepository ?? throw new ArgumentNullException(nameof(committeeMembershipRepository));
		_memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<ReportData> GenerateAsync(ReportFilter? filter = null)
	{
		try
		{
			// Get all members
			var members = await _memberRepository.GetAllAsync();
			var memberDict = members.ToDictionary(m => m.Id, m => m.Name);

			// Get all committee memberships
			var allMemberships = new List<Core.Entities.CommitteeMembership>();
			foreach (var member in members)
			{
				var history = await _committeeMembershipRepository.GetHistoryAsync(member.Id);
				allMemberships.AddRange(history);
			}

			// Organize by year, then by member name
			var membershipsByYear = allMemberships
				.Where(cm => memberDict.ContainsKey(cm.MemberId))
				.GroupBy(cm => cm.Year)
				.OrderByDescending(g => g.Key)
				.ToList();

			// Build report rows
			var rows = new List<string[]>();

			foreach (var yearGroup in membershipsByYear)
			{
				// Year header
				rows.Add(new[] { $"=== {yearGroup.Key} ===", "", "" });

				// Sort members by name within each year
				var sortedByName = yearGroup
					.OrderBy(cm => memberDict.ContainsKey(cm.MemberId) ? memberDict[cm.MemberId] : "Unknown")
					.ToList();

				foreach (var membership in sortedByName)
				{
					var memberName = memberDict.ContainsKey(membership.MemberId) 
						? memberDict[membership.MemberId] 
						: "Unknown";

					var position = string.IsNullOrEmpty(membership.Position) ? "(No Position)" : membership.Position;

					rows.Add(new[]
					{
						memberName,
						membership.Year.ToString(),
						position
					});
				}

				rows.Add(new[] { "", "", "" }); // Spacing between years
			}

			var totalMemberships = allMemberships.Count;
			var uniqueMembers = allMemberships.Select(cm => cm.MemberId).Distinct().Count();
			var latestYear = membershipsByYear.FirstOrDefault()?.Key ?? 0;

			_logger.LogInformation("Committee Report generated successfully with {TotalMemberships} memberships for {UniqueMembers} members", 
				totalMemberships, uniqueMembers);

			return new ReportData
			{
				ReportTitle = $"Committee Report - {DateTime.Now:MMMM d, yyyy}",
				ColumnHeaders = new[] { "Member Name", "Year", "Position" },
				Rows = rows.ToArray(),
				Summaries = new Dictionary<string, string>
				{
					{ "Total Memberships", totalMemberships.ToString() },
					{ "Unique Members", uniqueMembers.ToString() },
					{ "Latest Year", latestYear.ToString() },
					{ "Years Covered", membershipsByYear.Count.ToString() }
				},
				GeneratedAt = DateTime.UtcNow
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error generating Committee Report");
			throw;
		}
	}
}
