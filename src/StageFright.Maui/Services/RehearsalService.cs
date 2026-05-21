namespace StageFright.Maui.Services;

using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Services;
using StageFright.Data.Repositories;
using StageFright.Data.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

/// <summary>Service for rehearsal scheduling and batch attendance recording with atomic transactions.</summary>
public class RehearsalService : IRehearsalService
{
	private readonly IRehearsalRepository _rehearsalRepository;
	private readonly IAttendanceRepository _attendanceRepository;
	private readonly IMemberRepository _memberRepository;
	private readonly IFeeRepository _feeRepository;
	private readonly StageFrightContext _context;

	public RehearsalService(
		IRehearsalRepository rehearsalRepository,
		IAttendanceRepository attendanceRepository,
		IMemberRepository memberRepository,
		IFeeRepository feeRepository,
		StageFrightContext context)
	{
		_rehearsalRepository = rehearsalRepository;
		_attendanceRepository = attendanceRepository;
		_memberRepository = memberRepository;
		_feeRepository = feeRepository;
		_context = context;
	}

	public async Task<Rehearsal> ScheduleRehearsalAsync(DateTime date, TimeSpan time, string? notes = null)
	{
		if (date < DateTime.Today)
			throw new ValidationException("Rehearsal date cannot be in the past.");

		var rehearsal = new Rehearsal
		{
			Date = date,
			Time = time,
			Notes = notes ?? string.Empty,
			IsDeleted = false
		};

		await _rehearsalRepository.CreateAsync(rehearsal);
		return rehearsal;
	}

	public async Task<Rehearsal?> GetRehearsalByIdAsync(Guid id)
	{
		return await _rehearsalRepository.GetByIdAsync(id);
	}

	public async Task<IEnumerable<Rehearsal>> GetRehearsalsAsync(DateTime fromDate, DateTime toDate)
	{
		return await _rehearsalRepository.GetByDateRangeAsync(fromDate, toDate);
	}

	public async Task<Rehearsal?> GetMostRecentRehearsalAsync()
	{
		return await _rehearsalRepository.GetMostRecentAsync();
	}

	public async Task RecordAttendanceAsync(Guid rehearsalId, Guid memberId)
	{
		var rehearsal = await _rehearsalRepository.GetByIdAsync(rehearsalId);
		if (rehearsal == null)
			throw new EntityNotFoundException($"Rehearsal with ID {rehearsalId} not found.");

		var member = await _memberRepository.GetByIdAsync(memberId);
		if (member == null)
			throw new EntityNotFoundException($"Member with ID {memberId} not found.");

		await _attendanceRepository.RecordAsync(rehearsalId, memberId, "Paid");
	}

	public async Task<decimal> GetAttendanceRateAsync(Guid memberId, DateTime fromDate, DateTime toDate)
	{
		return await _attendanceRepository.GetAttendanceRateAsync(memberId, fromDate, toDate);
	}

	/// <summary>Records batch attendance with atomic transaction: creates all Attendance + Fee records together.</summary>
	public async Task RecordBatchAttendanceAsync(Guid rehearsalId, List<(Guid memberId, string paidStatus)> attendanceRecords)
	{
		var rehearsal = await _rehearsalRepository.GetByIdAsync(rehearsalId);
		if (rehearsal == null)
			throw new EntityNotFoundException($"Rehearsal with ID {rehearsalId} not found.");

		using var transaction = await _context.Database.BeginTransactionAsync();
		try
		{
			// Record all attendance
			var fees = new List<Fee>();
			foreach (var (memberId, paidStatus) in attendanceRecords)
			{
				var member = await _memberRepository.GetByIdAsync(memberId);
				if (member == null)
					throw new EntityNotFoundException($"Member with ID {memberId} not found.");

				// Record attendance
				await _attendanceRepository.RecordAsync(rehearsalId, memberId, paidStatus);

				// Create fee if paid
				if (paidStatus == "Paid")
				{
					var settings = await _context.Settings.FirstOrDefaultAsync();
					var attendanceFee = settings?.AttendanceFee ?? 0;

					var fee = new Fee
					{
						MemberId = memberId,
						FeeType = "Attendance",
						Amount = attendanceFee,
						FeeDate = rehearsal.Date,
						DueDate = rehearsal.Date.AddDays(30),
						CreatedAt = DateTime.UtcNow
					};
					fees.Add(fee);
				}
			}

			// Create all fees
			foreach (var fee in fees)
			{
				await _feeRepository.CreateAsync(fee);
			}

			// Calculate and store attendance rate
			var activeMembers = await _memberRepository.GetHistoricalActiveMembersAsync(rehearsal.Date);
			var activeMemberCount = activeMembers.Count();
			var presentCount = attendanceRecords.Count;
			var attendanceRate = activeMemberCount > 0 ? (decimal)presentCount / activeMemberCount * 100 : 0;

			await _rehearsalRepository.UpdateStoredAttendanceRateAsync(rehearsalId, attendanceRate);

			await transaction.CommitAsync();
		}
		catch
		{
			await transaction.RollbackAsync();
			throw;
		}
	}
}
