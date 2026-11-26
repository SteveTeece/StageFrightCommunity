using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Localization.Resources;
using StageFright.Core.Modules.Organisations;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Organisations;

/// <summary>
/// Unit tests for ExternalOrganisationService through IExternalOrganisationService (spec 032, US1):
/// create (FR-002), the active-name duplicate rule (FR-003), email validation at the service boundary
/// (FR-004), update (FR-005), archive/restore (FR-006) and the audit entry each write leaves (FR-014).
/// The stub localizer echoes the key, so a refusal's message pins the key used.
/// </summary>
public class ExternalOrganisationServiceTests : TestBase
{
    private readonly IExternalOrganisationRepository _repo = Substitute.For<IExternalOrganisationRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IExternalOrganisationService _svc;

    public ExternalOrganisationServiceTests()
    {
        // UnitOfWork just executes the operation inline for unit tests.
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _repo.AddAsync(Arg.Any<ExternalOrganisation>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<ExternalOrganisation>(0));

        var localizer = new StubStringLocalizer<ValidationResource>();
        _svc = new ExternalOrganisationService(
            _repo, new ExternalOrganisationValidationService(localizer), _audit, _unitOfWork, localizer);
    }

    private static ExternalOrganisation Organisation(string name = "City Council", bool archived = false) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Address = "1 Civic Pl",
        Email = "info@council.example",
        IsDeleted = archived,
        DeletedAt = archived ? DateTime.UtcNow : null,
        DeletedBy = archived ? "system" : null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private void NameInUse(string name, bool inUse) =>
        _repo.IsActiveNameInUseAsync(name, Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(inUse);

    // ── Reads ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnRepositoryActiveList_When_GetActiveAsyncCalled()
    {
        IReadOnlyList<ExternalOrganisation> active = [Organisation()];
        _repo.GetAllAsync(Arg.Any<CancellationToken>()).Returns(active);

        var result = await _svc.GetActiveAsync(Ct);

        Assert.Same(active, result);
    }

    [Fact]
    public async Task Should_ReturnRepositoryArchivedList_When_GetArchivedAsyncCalled()
    {
        IReadOnlyList<ExternalOrganisation> archived = [Organisation(archived: true)];
        _repo.GetArchivedAsync(Arg.Any<CancellationToken>()).Returns(archived);

        var result = await _svc.GetArchivedAsync(Ct);

        Assert.Same(archived, result);
    }

    [Fact]
    public async Task Should_ReturnArchivedOrganisation_When_GetByIdAsyncCalledForArchivedId()
    {
        var archived = Organisation(archived: true);
        _repo.GetByIdIncludingArchivedAsync(archived.Id, Arg.Any<CancellationToken>()).Returns(archived);

        var result = await _svc.GetByIdAsync(archived.Id, Ct);

        Assert.Same(archived, result);
    }

    [Fact]
    public async Task Should_ReturnNull_When_GetByIdAsyncCalledForUnknownId()
    {
        var result = await _svc.GetByIdAsync(Guid.NewGuid(), Ct);

        Assert.Null(result);
    }

    // ── Create (FR-002) ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_CreateOrganisation_When_OnlyNameGiven()
    {
        var created = await _svc.CreateAsync(new SaveExternalOrganisationRequest { Name = "Riverside Hall" }, Ct);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Riverside Hall", created.Name);
        Assert.Null(created.Address);
        Assert.Null(created.Email);
        Assert.False(created.IsDeleted);
        await _repo.Received(1).AddAsync(created, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_CreateTrimmedOrganisation_When_EveryFieldGiven()
    {
        var request = new SaveExternalOrganisationRequest
        {
            Name = "  Riverside Hall  ",
            Address = "  1 River Rd\nRiverside NSW 2000  ",
            Email = "  bookings@riverside.example  "
        };

        var created = await _svc.CreateAsync(request, Ct);

        Assert.Equal("Riverside Hall", created.Name);
        Assert.Equal("1 River Rd\nRiverside NSW 2000", created.Address);
        Assert.Equal("bookings@riverside.example", created.Email);
    }

    [Fact]
    public async Task Should_StoreNull_When_OptionalFieldsAreBlank()
    {
        var request = new SaveExternalOrganisationRequest { Name = "Riverside Hall", Address = "   ", Email = "" };

        var created = await _svc.CreateAsync(request, Ct);

        Assert.Null(created.Address);
        Assert.Null(created.Email);
    }

    [Fact]
    public async Task Should_RefuseCreate_When_NameIsBlank()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _svc.CreateAsync(new SaveExternalOrganisationRequest { Name = "   " }, Ct));

        Assert.Equal("Validation_Organisation_NameRequired", ex.Message);
        await _repo.DidNotReceive().AddAsync(Arg.Any<ExternalOrganisation>(), Arg.Any<CancellationToken>());
    }

    // ── Duplicate names (FR-003) ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_RefuseCreate_When_TrimmedNameMatchesActiveOrganisationIgnoringCase()
    {
        // The repository compares case-insensitively; the service must pass it the trimmed name.
        NameInUse("city council", true);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _svc.CreateAsync(new SaveExternalOrganisationRequest { Name = "city council " }, Ct));

        Assert.Equal("Validation_Organisation_NameDuplicate", ex.Message);
        await _repo.DidNotReceive().AddAsync(Arg.Any<ExternalOrganisation>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().LogAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<AuditAction>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AllowCreate_When_MatchingOrganisationIsArchived()
    {
        // Archived organisations never count as "in use", so the repository reports false.
        NameInUse("city council", false);

        var created = await _svc.CreateAsync(new SaveExternalOrganisationRequest { Name = "city council " }, Ct);

        Assert.Equal("city council", created.Name);
        await _repo.Received(1).IsActiveNameInUseAsync("city council", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RefuseUpdate_When_NameTakenByAnotherActiveOrganisation()
    {
        var org = Organisation("Riverside Hall");
        _repo.GetByIdIncludingArchivedAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);
        _repo.IsActiveNameInUseAsync("City Council", org.Id, Arg.Any<CancellationToken>()).Returns(true);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _svc.UpdateAsync(org.Id, new SaveExternalOrganisationRequest { Name = "City Council" }, Ct));

        Assert.Equal("Validation_Organisation_NameDuplicate", ex.Message);
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<ExternalOrganisation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RefuseRestore_When_NameNowUsedByActiveOrganisation()
    {
        var archived = Organisation(archived: true);
        _repo.GetByIdIncludingArchivedAsync(archived.Id, Arg.Any<CancellationToken>()).Returns(archived);
        _repo.IsActiveNameInUseAsync(archived.Name, archived.Id, Arg.Any<CancellationToken>()).Returns(true);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _svc.RestoreAsync(archived.Id, Ct));

        Assert.Equal("Validation_Organisation_NameDuplicate", ex.Message);
        await _repo.DidNotReceive().RestoreAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().LogAsync(Arg.Any<string>(), Arg.Any<Guid>(), AuditAction.Restore,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Email validation at the service boundary (FR-004) ────────────────────────────────────

    [Fact]
    public async Task Should_RefuseCreate_When_EmailIsInvalid()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _svc.CreateAsync(new SaveExternalOrganisationRequest { Name = "Riverside Hall", Email = "not-an-email" }, Ct));

        Assert.Equal("Validation_Organisation_EmailInvalid", ex.Message);
        await _repo.DidNotReceive().AddAsync(Arg.Any<ExternalOrganisation>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RefuseUpdate_When_EmailIsInvalid()
    {
        var org = Organisation();
        _repo.GetByIdIncludingArchivedAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _svc.UpdateAsync(org.Id, new SaveExternalOrganisationRequest { Name = org.Name, Email = "bad@" }, Ct));

        Assert.Equal("Validation_Organisation_EmailInvalid", ex.Message);
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<ExternalOrganisation>(), Arg.Any<CancellationToken>());
    }

    // ── Update (FR-005) ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_PersistTrimmedChanges_When_UpdateIsValid()
    {
        var org = Organisation();
        _repo.GetByIdIncludingArchivedAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);

        await _svc.UpdateAsync(org.Id, new SaveExternalOrganisationRequest
        {
            Name = " City Council ",
            Address = " 2 New St\nTown ",
            Email = " "
        }, Ct);

        await _repo.Received(1).UpdateAsync(Arg.Is<ExternalOrganisation>(o =>
            o!.Id == org.Id &&
            o.Name == "City Council" &&
            o.Address == "2 New St\nTown" &&
            o.Email == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_UpdatingUnknownOrganisation()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _svc.UpdateAsync(Guid.NewGuid(), new SaveExternalOrganisationRequest { Name = "Riverside Hall" }, Ct));
    }

    [Fact]
    public async Task Should_RefuseUpdate_When_OrganisationIsArchived()
    {
        var archived = Organisation(archived: true);
        _repo.GetByIdIncludingArchivedAsync(archived.Id, Arg.Any<CancellationToken>()).Returns(archived);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _svc.UpdateAsync(archived.Id, new SaveExternalOrganisationRequest { Name = "City Council" }, Ct));

        Assert.Equal("Validation_Organisation_Archived", ex.Message);
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<ExternalOrganisation>(), Arg.Any<CancellationToken>());
    }

    // ── Archive and restore (FR-006) ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_ArchiveThroughRepository_When_OrganisationIsActive()
    {
        var org = Organisation();
        _repo.GetByIdIncludingArchivedAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);

        await _svc.ArchiveAsync(org.Id, Ct);

        await _repo.Received(1).ArchiveAsync(org.Id, "system", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_ArchivingUnknownOrganisation()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _svc.ArchiveAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Should_RefuseArchive_When_OrganisationAlreadyArchived()
    {
        var archived = Organisation(archived: true);
        _repo.GetByIdIncludingArchivedAsync(archived.Id, Arg.Any<CancellationToken>()).Returns(archived);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _svc.ArchiveAsync(archived.Id, Ct));

        Assert.Equal("Validation_Organisation_Archived", ex.Message);
        await _repo.DidNotReceive().ArchiveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RestoreThroughRepository_When_NameIsFree()
    {
        var archived = Organisation(archived: true);
        _repo.GetByIdIncludingArchivedAsync(archived.Id, Arg.Any<CancellationToken>()).Returns(archived);
        NameInUse(archived.Name, false);

        await _svc.RestoreAsync(archived.Id, Ct);

        await _repo.Received(1).RestoreAsync(archived.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_RestoringUnknownOrganisation()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _svc.RestoreAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task Should_DoNothing_When_RestoringActiveOrganisation()
    {
        var org = Organisation();
        _repo.GetByIdIncludingArchivedAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);

        await _svc.RestoreAsync(org.Id, Ct);

        await _repo.DidNotReceive().RestoreAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().LogAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<AuditAction>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Audit trail (FR-014) ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_AuditCreate_When_OrganisationCreated()
    {
        var created = await _svc.CreateAsync(new SaveExternalOrganisationRequest { Name = "Riverside Hall" }, Ct);

        await _audit.Received(1).LogAsync(nameof(ExternalOrganisation), created.Id, AuditAction.Create,
            Arg.Any<string?>(), Arg.Is<string?>(v => v!.Contains("Riverside Hall")), Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AuditUpdateWithOldAndNewAddress_When_OrganisationUpdated()
    {
        var org = Organisation();
        _repo.GetByIdIncludingArchivedAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);

        await _svc.UpdateAsync(org.Id, new SaveExternalOrganisationRequest { Name = org.Name, Address = "2 New St" }, Ct);

        await _audit.Received(1).LogAsync(nameof(ExternalOrganisation), org.Id, AuditAction.Update,
            Arg.Is<string?>(v => v!.Contains("1 Civic Pl")), Arg.Is<string?>(v => v!.Contains("2 New St")),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AuditDelete_When_OrganisationArchived()
    {
        var org = Organisation();
        _repo.GetByIdIncludingArchivedAsync(org.Id, Arg.Any<CancellationToken>()).Returns(org);

        await _svc.ArchiveAsync(org.Id, Ct);

        await _audit.Received(1).LogAsync(nameof(ExternalOrganisation), org.Id, AuditAction.Delete,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AuditRestore_When_OrganisationRestored()
    {
        var archived = Organisation(archived: true);
        _repo.GetByIdIncludingArchivedAsync(archived.Id, Arg.Any<CancellationToken>()).Returns(archived);
        NameInUse(archived.Name, false);

        await _svc.RestoreAsync(archived.Id, Ct);

        await _audit.Received(1).LogAsync(nameof(ExternalOrganisation), archived.Id, AuditAction.Restore,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RunWritesInTransaction_When_OrganisationCreated()
    {
        await _svc.CreateAsync(new SaveExternalOrganisationRequest { Name = "Riverside Hall" }, Ct);

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>());
    }
}
