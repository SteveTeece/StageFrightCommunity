using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Localization;
using StageFright.Core.Modules.Members;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;
using StageFright.Reports.Rendering;
using StageFright.Reports.Resources;

namespace StageFright.Reports.Tests;

/// <summary>
/// T041 / FR-012 (spec 028): every financial-statement provider sets
/// <see cref="ReportData.BasisOfAccounting"/> from the single shared
/// <c>Reports_Common_BasisOfAccounting</c> string, whose wording names both the accrual
/// treatment of member fees and the cash treatment of other activity (never one blanket
/// basis). Member List and Committee — not financial statements — leave it null. The PDF
/// and CSV renderers carry a set basis line through to their output and omit it when null.
/// </summary>
public class BasisOfAccountingTests
{
    private static readonly ILocalizer L = RealLocalizer.Instance;
    private const string BasisKey = "Reports_Common_BasisOfAccounting";
    private static string ExpectedBasis => L.Get<ReportsResource>(BasisKey);

    // --- the shared string itself ---------------------------------------------------

    [Fact]
    public void SharedBasisString_ResolvesToRealText_NamingBothTreatments()
    {
        var basis = ExpectedBasis;

        Assert.False(string.IsNullOrWhiteSpace(basis));
        Assert.NotEqual(BasisKey, basis);                                  // not the raw-key echo
        Assert.DoesNotContain("Reports_", basis, StringComparison.Ordinal);
        Assert.Contains("accrual", basis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cash", basis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("member", basis, StringComparison.OrdinalIgnoreCase);
    }

    // --- the eight financial statements set it ------------------------------------------

    [Fact]
    public async Task IncomeStatement_SetsSharedBasisOfAccounting()
        => Assert.Equal(ExpectedBasis, (await GenerateIncomeStatement()).BasisOfAccounting);

    [Fact]
    public async Task BalanceSheet_SetsSharedBasisOfAccounting()
        => Assert.Equal(ExpectedBasis, (await GenerateBalanceSheet()).BasisOfAccounting);

    [Fact]
    public async Task TrialBalance_SetsSharedBasisOfAccounting()
        => Assert.Equal(ExpectedBasis, (await GenerateTrialBalance()).BasisOfAccounting);

    [Fact]
    public async Task TaxSummary_WhenTaxApplicable_SetsSharedBasisOfAccounting()
        => Assert.Equal(ExpectedBasis, (await GenerateTaxSummary(taxApplicable: true)).BasisOfAccounting);

    [Fact]
    public async Task TaxSummary_WhenTaxNotApplicable_SetsSharedBasisOfAccounting()
        => Assert.Equal(ExpectedBasis, (await GenerateTaxSummary(taxApplicable: false)).BasisOfAccounting);

    [Fact]
    public async Task AccountRegister_SetsSharedBasisOfAccounting()
        => Assert.Equal(ExpectedBasis, (await GenerateAccountRegister()).BasisOfAccounting);

    [Fact]
    public async Task GeneralLedger_SetsSharedBasisOfAccounting()
        => Assert.Equal(ExpectedBasis, (await GenerateGeneralLedger()).BasisOfAccounting);

    [Fact]
    public async Task BankReconciliation_SetsSharedBasisOfAccounting()
        => Assert.Equal(ExpectedBasis, (await GenerateBankReconciliation()).BasisOfAccounting);

    [Fact]
    public async Task MemberAccountSummary_SetsSharedBasisOfAccounting()
        => Assert.Equal(ExpectedBasis, (await GenerateMemberAccountSummary()).BasisOfAccounting);

    // --- the two non-financial reports leave it null ---------------------------------

    [Fact]
    public async Task MemberList_LeavesBasisOfAccountingNull()
        => Assert.Null((await GenerateMemberList()).BasisOfAccounting);

    [Fact]
    public async Task Committee_LeavesBasisOfAccountingNull()
        => Assert.Null((await GenerateCommittee()).BasisOfAccounting);

    // --- renderers carry a set basis line through ------------------------------------

    [Fact]
    public void PdfRenderer_WithBasisOfAccounting_RendersWithoutThrowing()
    {
        var bytes = new PdfReportRenderer(L).Render(ReportWithBasis(ExpectedBasis));

        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void CsvExporter_WithBasisOfAccounting_AppendsItAsTheTrailingRecord()
    {
        var csv = new CsvReportExporter().Export(ReportWithBasis(ExpectedBasis));
        var lines = csv.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        Assert.Contains("accrual", csv, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("accrual", lines[^1], StringComparison.OrdinalIgnoreCase); // last record
        Assert.Contains("cash", lines[^1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CsvExporter_WithoutBasisOfAccounting_AddsNoExtraRecord()
    {
        var withBasis = new CsvReportExporter().Export(ReportWithBasis(ExpectedBasis))
            .Split('\n').Count(l => !string.IsNullOrWhiteSpace(l));
        var withoutBasis = new CsvReportExporter().Export(ReportWithBasis(null))
            .Split('\n').Count(l => !string.IsNullOrWhiteSpace(l));

        Assert.Equal(withBasis - 1, withoutBasis);
        Assert.DoesNotContain("accrual", new CsvReportExporter().Export(ReportWithBasis(null)),
            StringComparison.OrdinalIgnoreCase);
    }

    // --- helpers --------------------------------------------------------------------

    private static ReportData ReportWithBasis(string? basis) => new()
    {
        Title = "Statement",
        SubTitle = "1 July 2025 – 30 June 2026",
        GeneratedAt = DateTime.UtcNow,
        Columns =
        [
            new ReportColumn { Header = "Account", Alignment = ReportColumnAlignment.Left },
            new ReportColumn { Header = "Amount", Alignment = ReportColumnAlignment.Right }
        ],
        Sections =
        [
            new ReportSection { Heading = "Income", Rows = [new ReportRow { Cells = ["Dues", "100.00"] }] }
        ],
        GrandTotal = new ReportRow { Cells = ["Total", "100.00"], IsEmphasized = true },
        BasisOfAccounting = basis
    };

    private static readonly ReportFilterValues NoFilters = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (IGLRepository gl, IAccountRepository accounts) EmptyGl()
    {
        var gl = Substitute.For<IGLRepository>();
        var accounts = Substitute.For<IAccountRepository>();
        accounts.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>([]));
        accounts.GetArchivedAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Account>>([]));
        gl.GetAccountMovementsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<Guid, (decimal Debits, decimal Credits)>>(
                new Dictionary<Guid, (decimal, decimal)>()));
        gl.GetByDateRangeAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Transaction>>([]));
        return (gl, accounts);
    }

    private static async Task<ReportData> GenerateIncomeStatement()
    {
        var (gl, accounts) = EmptyGl();
        return await new IncomeStatementReportProvider(gl, accounts, Substitute.For<ISettingsRepository>(), L)
            .GenerateAsync(NoFilters, Ct);
    }

    private static async Task<ReportData> GenerateBalanceSheet()
    {
        var (gl, accounts) = EmptyGl();
        return await new BalanceSheetReportProvider(gl, accounts, Substitute.For<ISettingsRepository>(), L)
            .GenerateAsync(NoFilters, Ct);
    }

    private static async Task<ReportData> GenerateTrialBalance()
    {
        var (gl, accounts) = EmptyGl();
        gl.GetBalanceTotalsAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((0m, 0m));
        return await new TrialBalanceReportProvider(gl, accounts, Substitute.For<ISettingsRepository>(), L)
            .GenerateAsync(NoFilters, Ct);
    }

    private static async Task<ReportData> GenerateTaxSummary(bool taxApplicable)
    {
        var (gl, accounts) = EmptyGl();
        var settings = Substitute.For<ISettingsRepository>();
        settings.GetAsync(Arg.Any<CancellationToken>()).Returns(taxApplicable
            ? new Settings { IsTaxApplicable = true, FinancialYearStartMonth = 7 }
            : (Settings?)null);
        return await new TaxSummaryReportProvider(gl, accounts, settings, L).GenerateAsync(NoFilters, Ct);
    }

    private static async Task<ReportData> GenerateAccountRegister()
    {
        var (gl, accounts) = EmptyGl();
        return await new AccountRegisterReportProvider(gl, accounts, L).GenerateAsync(NoFilters, Ct);
    }

    private static async Task<ReportData> GenerateGeneralLedger()
    {
        var (gl, accounts) = EmptyGl();
        return await new GeneralLedgerReportProvider(gl, accounts, L).GenerateAsync(NoFilters, Ct);
    }

    private static async Task<ReportData> GenerateBankReconciliation()
    {
        var (gl, accounts) = EmptyGl();
        return await new BankReconciliationReportProvider(
            Substitute.For<IBankReconciliationRepository>(), accounts, gl, L).GenerateAsync(NoFilters, Ct);
    }

    private static async Task<ReportData> GenerateMemberAccountSummary()
    {
        var members = Substitute.For<IMemberRepository>();
        members.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Member>>([]));
        return await new MemberAccountSummaryReportProvider(
            Substitute.For<IGLRepository>(), members, Substitute.For<IMemberBalanceService>(), L)
            .GenerateAsync(NoFilters, Ct);
    }

    private static async Task<ReportData> GenerateMemberList()
    {
        var members = Substitute.For<IMemberRepository>();
        members.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Member>>([]));
        return await new MemberListReportProvider(members, new AgeCalculationService(L), L)
            .GenerateAsync(NoFilters, Ct);
    }

    private static async Task<ReportData> GenerateCommittee()
    {
        var members = Substitute.For<IMemberRepository>();
        members.GetByStatusAsync(MemberStatus.Active, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Member>>([]));
        return await new CommitteeReportProvider(
            Substitute.For<ICommitteePositionRecordRepository>(), members, L).GenerateAsync(NoFilters, Ct);
    }
}
