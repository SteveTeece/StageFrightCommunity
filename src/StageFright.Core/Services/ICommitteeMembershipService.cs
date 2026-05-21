namespace StageFright.Core.Services;

using Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Service for committee membership tracking.</summary>
public interface ICommitteeMembershipService
{
	Task<IEnumerable<CommitteeMembership>> GetMemberCommitteeHistoryAsync(Guid memberId);
	Task<IEnumerable<CommitteeMembership>> GetCommitteeForYearAsync(int year);
	Task RecordCommitteeMembershipAsync(Guid memberId, int year, string position);
	Task ResetCommitteeYearAsync(int year);
	Task<CommitteeMembership?> GetCurrentCommitteeMembershipAsync(Guid memberId);
}
