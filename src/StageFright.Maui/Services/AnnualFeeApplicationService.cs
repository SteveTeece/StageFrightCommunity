namespace StageFright.Maui.Services;

using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Services;
using StageFright.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Service for annual fee application batch processing.</summary>
public class AnnualFeeApplicationService : IAnnualFeeApplicationService
{
    private readonly IMemberRepository _memberRepository;
    private readonly IFeeRepository _feeRepository;
    private readonly ISettingsRepository _settingsRepository;

    public AnnualFeeApplicationService(
        IMemberRepository memberRepository,
        IFeeRepository feeRepository,
        ISettingsRepository settingsRepository)
    {
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
        _feeRepository = feeRepository ?? throw new ArgumentNullException(nameof(feeRepository));
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
    }

    public async Task<int> ApplyAnnualFeesAsync()
    {
        var settings = await _settingsRepository.GetSettingsAsync();
        if (settings == null)
            throw new InvalidOperationException("Settings not configured.");

        // Get all active members
        var activeMembers = (await _memberRepository.GetActiveMembersAsync()).ToList();
        var feesApplied = 0;

        foreach (var member in activeMembers)
        {
            // Check if member already has an unpaid annual fee from the current renewal period
            var currentYearFees = await _feeRepository.GetByYearAsync(DateTime.Today.Year);
            var memberHasUnpaidAnnualFee = currentYearFees.Any(f =>
                f.MemberId == member.Id &&
                f.FeeType == "Annual" &&
                f.Amount == settings.AnnualFee);

            // Skip if already has unpaid annual fee
            if (memberHasUnpaidAnnualFee)
                continue;

            // Create new annual fee
            var fee = new Fee
            {
                Id = Guid.NewGuid(),
                MemberId = member.Id,
                FeeType = "Annual",
                Amount = settings.AnnualFee,
                FeeDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(30),
                CreatedAt = DateTime.Now
            };

            await _feeRepository.CreateAsync(fee);
            feesApplied++;
        }

        return feesApplied;
    }

    public async Task<int> GetEligibleMemberCountAsync()
    {
        var settings = await _settingsRepository.GetSettingsAsync();
        if (settings == null)
            throw new InvalidOperationException("Settings not configured.");

        var activeMembers = (await _memberRepository.GetActiveMembersAsync()).ToList();
        var eligibleCount = 0;

        foreach (var member in activeMembers)
        {
            // Check if member already has an unpaid annual fee from the current renewal period
            var currentYearFees = await _feeRepository.GetByYearAsync(DateTime.Today.Year);
            var memberHasUnpaidAnnualFee = currentYearFees.Any(f =>
                f.MemberId == member.Id &&
                f.FeeType == "Annual" &&
                f.Amount == settings.AnnualFee);

            // Count if doesn't have unpaid annual fee
            if (!memberHasUnpaidAnnualFee)
                eligibleCount++;
        }

        return eligibleCount;
    }

    public async Task<decimal> GetAnnualFeeAmountAsync()
    {
        var settings = await _settingsRepository.GetSettingsAsync();
        if (settings == null)
            throw new InvalidOperationException("Settings not configured.");

        return settings.AnnualFee;
    }
}
