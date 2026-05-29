namespace StageFright.Reports.Providers;

using Microsoft.Extensions.Logging;
using StageFright.Data.Repositories;
using StageFright.Data.Services;
using StageFright.Plugins.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Member Account Summary report provider showing member balances with aging buckets.
/// Includes archived members and supports member filtering.
/// </summary>
public class MemberAccountSummaryReportProvider : IReportProvider
{
	private readonly IMemberRepository _memberRepository;
	private readonly IFeeRepository _feeRepository;
	private readonly IPaymentRepository _paymentRepository;
	private readonly MemberBalanceService _memberBalanceService;
	private readonly ILogger<MemberAccountSummaryReportProvider> _logger;

	public string ModuleName => "Finance";
	public string ReportId => "member-account-summary";
	public string ReportName => "Member Account Summary";
	public int DisplayOrder => 4;

	public MemberAccountSummaryReportProvider(
		IMemberRepository memberRepository,
		IFeeRepository feeRepository,
		IPaymentRepository paymentRepository,
		MemberBalanceService memberBalanceService,
		ILogger<MemberAccountSummaryReportProvider> logger)
	{
		_memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
		_feeRepository = feeRepository ?? throw new ArgumentNullException(nameof(feeRepository));
		_paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
		_memberBalanceService = memberBalanceService ?? throw new ArgumentNullException(nameof(memberBalanceService));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public async Task<ReportData> GenerateAsync(ReportFilter? filter = null)
	{
		try
		{
			var referenceDate = filter?.DateTo ?? DateTime.Now;

			// Get all members (including inactive/archived)
			var allMembers = await _memberRepository.GetAllAsync();
			var members = allMembers.OrderBy(m => m.Name).ToList();

			// Build report rows
			var rows = new List<string[]>();
			decimal totalOutstanding = 0m;
			var agingBuckets = new Dictionary<string, decimal>
			{
				{ "Current (0-30 days)", 0m },
				{ "31-60 days", 0m },
				{ "61-90 days", 0m },
				{ "90+ days", 0m }
			};

			foreach (var member in members)
			{
				// Get member balance
				var balance = await _memberBalanceService.GetMemberBalanceAsync(member.Id);

				if (balance == 0) continue;

				// Get unpaid fees to calculate aging
				var unpaidFees = await _feeRepository.GetUnpaidAsync(member.Id);
				var oldestFeeDate = unpaidFees.Any() 
					? unpaidFees.Min(f => f.FeeDate)
					: referenceDate;

				var daysOld = (referenceDate - oldestFeeDate).TotalDays;
				string agingBucket;

				if (daysOld <= 30)
					agingBucket = "Current (0-30 days)";
				else if (daysOld <= 60)
					agingBucket = "31-60 days";
				else if (daysOld <= 90)
					agingBucket = "61-90 days";
				else
					agingBucket = "90+ days";

				agingBuckets[agingBucket] += balance;
				totalOutstanding += balance;

				var memberStatus = member.Status == "Active" ? "Active" : "Inactive";
				rows.Add(new[]
				{
					member.Name,
					memberStatus,
					unpaidFees.Count().ToString(),
					balance.ToString("C", CultureInfo.CurrentCulture),
					agingBucket
				});
			}

			// Sort by balance descending (highest outstanding first)
			var sortedRows = rows.OrderByDescending(r => 
			{
				if (decimal.TryParse(r[3].Replace("$", "").Replace(",", ""), out var amount))
					return amount;
				return 0m;
			}).ToList();

			_logger.LogInformation("Member Account Summary generated successfully with {MemberCount} members owing {TotalOutstanding:C}", 
				sortedRows.Count, totalOutstanding);

			return new ReportData
			{
				ReportTitle = $"Member Account Summary - As of {referenceDate:MMMM d, yyyy}",
				ColumnHeaders = new[] { "Member Name", "Status", "Unpaid Fees", "Outstanding Balance", "Aging" },
				Rows = sortedRows.ToArray(),
				Summaries = new Dictionary<string, string>
				{
					{ "Total Outstanding", totalOutstanding.ToString("C", CultureInfo.CurrentCulture) },
					{ "Current (0-30 days)", agingBuckets["Current (0-30 days)"].ToString("C", CultureInfo.CurrentCulture) },
					{ "31-60 days", agingBuckets["31-60 days"].ToString("C", CultureInfo.CurrentCulture) },
					{ "61-90 days", agingBuckets["61-90 days"].ToString("C", CultureInfo.CurrentCulture) },
					{ "90+ days", agingBuckets["90+ days"].ToString("C", CultureInfo.CurrentCulture) },
					{ "Members with Balance", sortedRows.Count.ToString() }
				},
				GeneratedAt = DateTime.UtcNow
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error generating Member Account Summary report");
			throw;
		}
	}
}
