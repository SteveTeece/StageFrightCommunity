namespace StageFright.Core.Services;

using System.Threading.Tasks;

/// <summary>Service for first-run setup.</summary>
public interface ISetupService
{
	Task InitializeApplicationAsync(string organizationName, decimal annualFee, decimal attendanceFee, int renewalMonth);
}
