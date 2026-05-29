namespace StageFright.Reports.Providers;

using Microsoft.Extensions.Logging;
using StageFright.Core.Services;
using StageFright.Data.Repositories;
using StageFright.Plugins.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Member List report provider showing member directory with contact and join information.
/// Supports status filtering (Active/Inactive).
/// </summary>
public class MemberListReportProvider : IReportProvider
{
	private readonly IMemberRepository _memberRepository;
	private readonly AgeCalculationService _ageCalculationService;
	private readonly ILogger<MemberListReportProvider> _logger;

	public string ModuleName => "Members";
	public string ReportId => "member-list";
	public string ReportName => "Member List";
	public int DisplayOrder => 1;

	public MemberListReportProvider(
		IMemberRepository memberRepository,
		AgeCalculationService ageCalculationService,
		ILogger<MemberListReportProvider> logger)
	{
		_memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
		_ageCalculationService = ageCalculationService ?? throw new ArgumentNullException(nameof(ageCalculationService));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<ReportData> GenerateAsync(ReportFilter? filter = null)
	{
		try
		{
			// Get members based on status filter
			List<Core.Entities.Member> members;
			string filterDescription = "All Members";

			if (!string.IsNullOrEmpty(filter?.MemberStatusFilter))
			{
				if (filter.MemberStatusFilter == "Active")
				{
					members = (await _memberRepository.GetActiveMembersAsync()).OrderBy(m => m.Name).ToList();
					filterDescription = "Active Members";
				}
				else if (filter.MemberStatusFilter == "Inactive")
				{
					members = (await _memberRepository.GetInactiveMembersAsync()).OrderBy(m => m.Name).ToList();
					filterDescription = "Inactive Members";
				}
				else
				{
					members = (await _memberRepository.GetAllAsync()).OrderBy(m => m.Name).ToList();
				}
			}
			else
			{
				members = (await _memberRepository.GetAllAsync()).OrderBy(m => m.Name).ToList();
			}

			// Build report rows
			var rows = new List<string[]>();

			foreach (var member in members)
			{
				var age = member.DateOfBirth.HasValue 
					? _ageCalculationService.CalculateAge(member.DateOfBirth.Value).ToString()
					: "-";

				var joinDate = member.JoinDate.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);

				rows.Add(new[]
				{
					member.Name,
					member.StreetAddress ?? "",
					member.Phone ?? "",
					member.Email ?? "",
					joinDate,
					age,
					member.Status
				});
			}

			_logger.LogInformation("Member List generated successfully with {MemberCount} members", members.Count);

			return new ReportData
			{
				ReportTitle = $"{filterDescription} - {DateTime.Now:MMMM d, yyyy}",
				ColumnHeaders = new[] { "Name", "Address", "Phone", "Email", "Join Date", "Age", "Status" },
				Rows = rows.ToArray(),
				Summaries = new Dictionary<string, string>
				{
					{ "Total Members", members.Count.ToString() },
					{ "Active Members", members.Count(m => m.Status == "Active").ToString() },
					{ "Inactive Members", members.Count(m => m.Status == "Inactive").ToString() }
				},
				GeneratedAt = DateTime.UtcNow
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error generating Member List report");
			throw;
		}
	}
}
