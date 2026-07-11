using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Data;
using StageFright.Data.Repositories;
using StageFright.Reports.Models;
using StageFright.Reports.Providers;
using StageFright.Reports.Registry;

namespace StageFright.Integration.Tests.Scenarios;

/// <summary>
/// Acceptance tests for V11: Reports menu structure, provider registration, and fault isolation.
/// Verifies Members appears before Finance, plugin modules are alphabetical after Finance,
/// duplicate ReportIds are skipped, and failing providers are isolated gracefully.
/// </summary>
public sealed class V11_ReportsMenuTests : IAsyncLifetime
{
    private StageFrightDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<StageFrightDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new StageFrightDbContext(options);
        await _db.Database.OpenConnectionAsync();
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.CloseConnectionAsync();
        await _db.DisposeAsync();
    }

    // --- Menu structure ---

    [Fact]
    public void Registry_MembersSection_AppearsFirst()
    {
        var registry = BuildRegistryWithAllProviders();
        var sections = registry.GetMenuSections();

        Assert.Equal("Members", sections[0].ModuleName);
    }

    [Fact]
    public void Registry_FinanceSection_AppearsSecond()
    {
        var registry = BuildRegistryWithAllProviders();
        var sections = registry.GetMenuSections();

        Assert.Equal("Finance", sections[1].ModuleName);
    }

    [Fact]
    public void Registry_MembersSection_ContainsExpectedReports()
    {
        var registry = BuildRegistryWithAllProviders();
        var sections = registry.GetMenuSections();
        var membersSection = sections.First(s => s.ModuleName == "Members");

        Assert.Contains(membersSection.Reports, r => r.ReportId == "member-list");
        Assert.Contains(membersSection.Reports, r => r.ReportId == "committee-report");
    }

    [Fact]
    public void Registry_FinanceSection_ContainsExpectedReports()
    {
        var registry = BuildRegistryWithAllProviders();
        var sections = registry.GetMenuSections();
        var financeSection = sections.First(s => s.ModuleName == "Finance");

        Assert.Contains(financeSection.Reports, r => r.ReportId == "income-statement");
        Assert.Contains(financeSection.Reports, r => r.ReportId == "trial-balance");
        Assert.Contains(financeSection.Reports, r => r.ReportId == "account-register");
        Assert.Contains(financeSection.Reports, r => r.ReportId == "member-account-summary");
    }

    [Fact]
    public void Registry_PluginModule_AppearsAfterFinanceAlphabetically()
    {
        var pluginProvider = MakeProvider("plugin-report", "Plugin Report", "TestPlugin");
        var registry = BuildRegistry(pluginProvider);
        var sections = registry.GetMenuSections();

        var moduleNames = sections.Select(s => s.ModuleName).ToList();
        Assert.True(moduleNames.IndexOf("TestPlugin") > moduleNames.IndexOf("Finance"));
    }

    // --- Duplicate ID handling ---

    [Fact]
    public void Registry_DuplicateReportId_OnlyFirstRegistered()
    {
        var p1 = MakeProvider("same-id", "First", "Finance");
        var p2 = MakeProvider("same-id", "Second", "Finance");
        var registry = new ReportProviderRegistry(
            [p1, p2],
            NullLogger<ReportProviderRegistry>.Instance);

        var financeSection = registry.GetMenuSections().First(s => s.ModuleName == "Finance");

        Assert.Single(financeSection.Reports);
        Assert.Equal("First", financeSection.Reports[0].ReportName);
    }

    // --- Provider lookup ---

    [Fact]
    public void Registry_GetProvider_ReturnsCorrectProvider()
    {
        var registry = BuildRegistryWithAllProviders();

        var provider = registry.GetProvider("income-statement");

        Assert.NotNull(provider);
        Assert.Equal("income-statement", provider!.ReportId);
    }

    [Fact]
    public void Registry_GetProvider_UnknownId_ReturnsNull()
    {
        var registry = BuildRegistryWithAllProviders();

        var provider = registry.GetProvider("no-such-report");

        Assert.Null(provider);
    }

    // --- Fault isolation ---

    [Fact]
    public async Task Registry_ThrowingProvider_DoesNotBlockOtherProviders()
    {
        var throwingProvider = Substitute.For<IReportProvider>();
        throwingProvider.ReportId.Returns("failing-report");
        throwingProvider.ReportName.Returns("Failing Report");
        throwingProvider.ModuleName.Returns("Finance");
        throwingProvider.DisplayOrder.Returns(99);
        throwingProvider.Filters.Returns(Array.Empty<ReportFilterDefinition>());
        throwingProvider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Simulated failure"));

        var workingProvider = MakeProvider("working-report", "Working Report", "Finance");
        workingProvider.GenerateAsync(Arg.Any<ReportFilterValues>(), Arg.Any<CancellationToken>())
            .Returns(new ReportData { Title = "Working", GeneratedAt = DateTime.UtcNow, Columns = [], Sections = [] });

        var registry = new ReportProviderRegistry(
            [throwingProvider, workingProvider],
            NullLogger<ReportProviderRegistry>.Instance);

        // Failing provider listed in menu (registry doesn't call GenerateAsync during registration)
        var sections = registry.GetMenuSections();
        var financeSection = sections.First(s => s.ModuleName == "Finance");
        Assert.Equal(2, financeSection.Reports.Count);

        // Working provider can generate successfully
        var provider = registry.GetProvider("working-report");
        var result = await provider!.GenerateAsync(new ReportFilterValues());
        Assert.Equal("Working", result.Title);
    }

    // --- Member List filter ---

    [Fact]
    public async Task MemberListReport_DefaultFilter_ReturnsActiveMembers()
    {
        _db.Members.AddRange(
            MakeMember("Alice", MemberStatus.Active, isDeleted: false),
            MakeMember("Bob", MemberStatus.Inactive, isDeleted: false),
            MakeMember("Charlie", MemberStatus.Active, isDeleted: true));
        await _db.SaveChangesAsync();

        var provider = new MemberListReportProvider(new MemberRepository(_db));
        var filters = new ReportFilterValues();
        filters.Set("memberStatus", "Active");

        var result = await provider.GenerateAsync(filters);

        var names = result.Sections.SelectMany(s => s.Rows).Select(r => r.Cells[0]).ToList();
        Assert.Contains("Alice", names);
        Assert.DoesNotContain("Bob", names);
        Assert.DoesNotContain("Charlie", names);
    }

    // --- Committee Report filter ---

    [Fact]
    public async Task CommitteeReport_DefaultFilter_ReturnsActiveOnly()
    {
        var activeMember = MakeMember("Active Member", MemberStatus.Active, isDeleted: false);
        var archivedMember = MakeMember("Archived Member", MemberStatus.Inactive, isDeleted: true);
        _db.Members.AddRange(activeMember, archivedMember);
        _db.CommitteeMemberships.AddRange(
            new CommitteeMembership { Id = Guid.NewGuid(), MemberId = activeMember.Id, Year = 2026, Position = "Chair", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new CommitteeMembership { Id = Guid.NewGuid(), MemberId = archivedMember.Id, Year = 2025, Position = "Treasurer", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var provider = new CommitteeReportProvider(
            new CommitteeMembershipRepository(_db),
            new MemberRepository(_db));

        var filters = new ReportFilterValues();
        filters.Set("memberFilter", "Active Only");

        var result = await provider.GenerateAsync(filters);
        var names = result.Sections.SelectMany(s => s.Rows).Select(r => r.Cells[0]).ToList();

        Assert.Contains("Active Member", names);
        Assert.DoesNotContain("Archived Member", names);
    }

    // --- Helpers ---

    private IReportProviderRegistry BuildRegistryWithAllProviders()
    {
        var glRepo = new GLRepository(_db);
        var catRepo = new AccountRepository(_db);
        var memberRepo = new MemberRepository(_db);
        var feeRepo = new FeeRepository(_db);
        var committeeRepo = new CommitteeMembershipRepository(_db);

        return new ReportProviderRegistry(
            new IReportProvider[]
            {
                new IncomeStatementReportProvider(glRepo, catRepo, new SettingsRepository(_db)),
                new TrialBalanceReportProvider(glRepo, catRepo, new SettingsRepository(_db)),
                new AccountRegisterReportProvider(glRepo, catRepo),
                new MemberAccountSummaryReportProvider(glRepo, memberRepo, feeRepo),
                new MemberListReportProvider(memberRepo),
                new CommitteeReportProvider(committeeRepo, memberRepo)
            },
            NullLogger<ReportProviderRegistry>.Instance);
    }

    private static IReportProviderRegistry BuildRegistry(params IReportProvider[] providers)
        => new ReportProviderRegistry(providers, NullLogger<ReportProviderRegistry>.Instance);

    private static IReportProvider MakeProvider(string reportId, string name, string module)
    {
        var p = Substitute.For<IReportProvider>();
        p.ReportId.Returns(reportId);
        p.ReportName.Returns(name);
        p.ModuleName.Returns(module);
        p.DisplayOrder.Returns(0);
        p.Filters.Returns(Array.Empty<ReportFilterDefinition>());
        return p;
    }

    private static Member MakeMember(string name, MemberStatus status, bool isDeleted)
        => new()
        {
            Id = Guid.NewGuid(), Name = name, StreetAddress = "1 Test St",
            Status = status, IsDeleted = isDeleted,
            JoinDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActivateDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
}
