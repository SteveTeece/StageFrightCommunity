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
/// Unit tests for ExternalOrganisationContactService through IExternalOrganisationContactService
/// (spec 032, US2): add and edit with validation (FR-008), remove as a soft delete (FR-009), the
/// archived-organisation refusal, and the audit entry each write leaves (FR-014). The stub localizer
/// echoes the key, so a refusal's message pins the key used. That a removed contact drops out of the
/// organisation's Contacts but stays in the table is proven against SQLite in
/// ExternalOrganisationContactRepositoryTests.
/// </summary>
public class ExternalOrganisationContactServiceTests : TestBase
{
    private readonly IExternalOrganisationContactRepository _contacts = Substitute.For<IExternalOrganisationContactRepository>();
    private readonly IExternalOrganisationRepository _organisations = Substitute.For<IExternalOrganisationRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IExternalOrganisationContactService _svc;

    private readonly ExternalOrganisation _organisation = Organisation(archived: false);

    public ExternalOrganisationContactServiceTests()
    {
        // UnitOfWork just executes the operation inline for unit tests.
        _unitOfWork
            .ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<Func<CancellationToken, Task>>(0)(ci.ArgAt<CancellationToken>(1)));

        _contacts.AddAsync(Arg.Any<ExternalOrganisationContact>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<ExternalOrganisationContact>(0));
        _organisations.GetByIdIncludingArchivedAsync(_organisation.Id, Arg.Any<CancellationToken>())
            .Returns(_organisation);

        var localizer = new StubStringLocalizer<ValidationResource>();
        _svc = new ExternalOrganisationContactService(
            _contacts, _organisations, new ExternalOrganisationValidationService(localizer), _audit, _unitOfWork, localizer);
    }

    private static ExternalOrganisation Organisation(bool archived) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Riverside Hall",
        IsDeleted = archived,
        DeletedAt = archived ? DateTime.UtcNow : null,
        DeletedBy = archived ? "system" : null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private ExternalOrganisationContact ExistingContact(ExternalOrganisation? organisation = null)
    {
        var contact = new ExternalOrganisationContact
        {
            Id = Guid.NewGuid(),
            ExternalOrganisationId = (organisation ?? _organisation).Id,
            Name = "Jo Bloggs",
            Role = "Venue manager",
            Phone = "02 9000 0000",
            Mobile = "0400 000 000",
            Email = "jo@riverside.example",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _contacts.GetByIdAsync(contact.Id, Arg.Any<CancellationToken>()).Returns(contact);
        return contact;
    }

    private static SaveExternalOrganisationContactRequest FullRequest() => new()
    {
        Name = "  Sam Lee  ",
        Role = "  Marketing officer  ",
        Phone = "  02 9111 1111  ",
        Mobile = "  0411 111 111  ",
        Email = "  sam@riverside.example  "
    };

    private async Task AssertNoWriteAsync()
    {
        await _contacts.DidNotReceive().AddAsync(Arg.Any<ExternalOrganisationContact>(), Arg.Any<CancellationToken>());
        await _contacts.DidNotReceive().UpdateAsync(Arg.Any<ExternalOrganisationContact>(), Arg.Any<CancellationToken>());
        await _contacts.DidNotReceive().ArchiveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().LogAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<AuditAction>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Add (FR-008) ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_AddTrimmedContactToOrganisation_When_AllFiveFieldsGiven()
    {
        var added = await _svc.AddAsync(_organisation.Id, FullRequest(), Ct);

        Assert.NotEqual(Guid.Empty, added.Id);
        Assert.Equal(_organisation.Id, added.ExternalOrganisationId);
        Assert.Equal("Sam Lee", added.Name);
        Assert.Equal("Marketing officer", added.Role);
        Assert.Equal("02 9111 1111", added.Phone);
        Assert.Equal("0411 111 111", added.Mobile);
        Assert.Equal("sam@riverside.example", added.Email);
        Assert.False(added.IsDeleted);
        await _contacts.Received(1).AddAsync(added, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AddContactWithNullOptionalFields_When_OnlyNameGiven()
    {
        var request = new SaveExternalOrganisationContactRequest { Name = "Sam Lee", Role = "  ", Phone = "", Email = " " };

        var added = await _svc.AddAsync(_organisation.Id, request, Ct);

        Assert.Equal("Sam Lee", added.Name);
        Assert.Null(added.Role);
        Assert.Null(added.Phone);
        Assert.Null(added.Mobile);
        Assert.Null(added.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_RefuseAdd_When_NameIsBlank(string name)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _svc.AddAsync(_organisation.Id, new SaveExternalOrganisationContactRequest { Name = name }, Ct));

        Assert.Equal("Validation_OrganisationContact_NameRequired", ex.Message);
        await AssertNoWriteAsync();
    }

    [Fact]
    public async Task Should_RefuseAdd_When_EmailIsInvalid()
    {
        var request = new SaveExternalOrganisationContactRequest { Name = "Sam Lee", Email = "not-an-email" };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _svc.AddAsync(_organisation.Id, request, Ct));

        Assert.Equal("Validation_OrganisationContact_EmailInvalid", ex.Message);
        await AssertNoWriteAsync();
    }

    [Fact]
    public async Task Should_RefuseAdd_When_OrganisationIsArchived()
    {
        var archived = Organisation(archived: true);
        _organisations.GetByIdIncludingArchivedAsync(archived.Id, Arg.Any<CancellationToken>()).Returns(archived);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _svc.AddAsync(archived.Id, FullRequest(), Ct));

        Assert.Equal("Validation_Organisation_Archived", ex.Message);
        await AssertNoWriteAsync();
    }

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_AddingToUnknownOrganisation()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _svc.AddAsync(Guid.NewGuid(), FullRequest(), Ct));

        await AssertNoWriteAsync();
    }

    // ── Edit (FR-008) ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_PersistTrimmedChanges_When_ContactEdited()
    {
        var contact = ExistingContact();

        await _svc.UpdateAsync(contact.Id, FullRequest(), Ct);

        await _contacts.Received(1).UpdateAsync(Arg.Is<ExternalOrganisationContact>(c =>
            c!.Id == contact.Id &&
            c.ExternalOrganisationId == _organisation.Id &&
            c.Name == "Sam Lee" &&
            c.Role == "Marketing officer" &&
            c.Phone == "02 9111 1111" &&
            c.Mobile == "0411 111 111" &&
            c.Email == "sam@riverside.example"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_ClearOptionalFields_When_EditLeavesThemBlank()
    {
        var contact = ExistingContact();

        await _svc.UpdateAsync(contact.Id, new SaveExternalOrganisationContactRequest { Name = "Jo Bloggs" }, Ct);

        await _contacts.Received(1).UpdateAsync(Arg.Is<ExternalOrganisationContact>(c =>
            c!.Role == null && c.Phone == null && c.Mobile == null && c.Email == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RefuseEdit_When_EmailIsInvalid()
    {
        var contact = ExistingContact();
        var request = new SaveExternalOrganisationContactRequest { Name = "Jo Bloggs", Email = "jo@nowhere" };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _svc.UpdateAsync(contact.Id, request, Ct));

        Assert.Equal("Validation_OrganisationContact_EmailInvalid", ex.Message);
        Assert.Equal("jo@riverside.example", contact.Email);
        await AssertNoWriteAsync();
    }

    [Fact]
    public async Task Should_RefuseEdit_When_OrganisationIsArchived()
    {
        var archived = Organisation(archived: true);
        _organisations.GetByIdIncludingArchivedAsync(archived.Id, Arg.Any<CancellationToken>()).Returns(archived);
        var contact = ExistingContact(archived);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _svc.UpdateAsync(contact.Id, FullRequest(), Ct));

        Assert.Equal("Validation_Organisation_Archived", ex.Message);
        await AssertNoWriteAsync();
    }

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_EditingUnknownOrRemovedContact()
    {
        // A removed contact is hidden by the soft-delete filter, so it reads as unknown.
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _svc.UpdateAsync(Guid.NewGuid(), FullRequest(), Ct));

        await AssertNoWriteAsync();
    }

    // ── Remove (FR-009) ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_SoftDeleteContact_When_ContactRemoved()
    {
        var contact = ExistingContact();

        await _svc.RemoveAsync(contact.Id, Ct);

        await _contacts.Received(1).ArchiveAsync(contact.Id, "system", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_RefuseRemove_When_OrganisationIsArchived()
    {
        var archived = Organisation(archived: true);
        _organisations.GetByIdIncludingArchivedAsync(archived.Id, Arg.Any<CancellationToken>()).Returns(archived);
        var contact = ExistingContact(archived);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _svc.RemoveAsync(contact.Id, Ct));

        Assert.Equal("Validation_Organisation_Archived", ex.Message);
        await AssertNoWriteAsync();
    }

    [Fact]
    public async Task Should_ThrowEntityNotFound_When_RemovingUnknownOrRemovedContact()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _svc.RemoveAsync(Guid.NewGuid(), Ct));

        await AssertNoWriteAsync();
    }

    // ── Audit trail (FR-014) ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_AuditCreate_When_ContactAdded()
    {
        var added = await _svc.AddAsync(_organisation.Id, FullRequest(), Ct);

        await _audit.Received(1).LogAsync(nameof(ExternalOrganisationContact), added.Id, AuditAction.Create,
            Arg.Any<string?>(), Arg.Is<string?>(v => v!.Contains("Sam Lee")), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AuditUpdateWithOldAndNewValues_When_ContactEdited()
    {
        var contact = ExistingContact();

        await _svc.UpdateAsync(contact.Id, FullRequest(), Ct);

        await _audit.Received(1).LogAsync(nameof(ExternalOrganisationContact), contact.Id, AuditAction.Update,
            Arg.Is<string?>(v => v!.Contains("Jo Bloggs")), Arg.Is<string?>(v => v!.Contains("Sam Lee")),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_AuditDelete_When_ContactRemoved()
    {
        var contact = ExistingContact();

        await _svc.RemoveAsync(contact.Id, Ct);

        await _audit.Received(1).LogAsync(nameof(ExternalOrganisationContact), contact.Id, AuditAction.Delete,
            Arg.Is<string?>(v => v!.Contains("Jo Bloggs")), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
