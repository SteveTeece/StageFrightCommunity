using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Modules.Members;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.Members;

/// <summary>
/// Unit tests for CommitteeOfficeHolderTypeService — built-in President/Secretary/Treasurer
/// guards (FR-013) and custom-title add/rename/reorder/archive (FR-012).
/// </summary>
public class CommitteeOfficeHolderTypeServiceTests : TestBase
{
    private readonly ICommitteeOfficeHolderTypeRepository _repo = Substitute.For<ICommitteeOfficeHolderTypeRepository>();
    private readonly IAuditTrailService _audit = Substitute.For<IAuditTrailService>();

    public CommitteeOfficeHolderTypeServiceTests()
    {
        _repo.AddAsync(Arg.Any<CommitteeOfficeHolderType>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<CommitteeOfficeHolderType>(0));
    }

    private CommitteeOfficeHolderTypeService CreateService() => new(_repo, _audit, RealLocalizer.Instance);

    private static CommitteeOfficeHolderType BuiltIn(string name, int order) => new()
    {
        Id = Guid.NewGuid(), Name = name, DisplayOrder = order, IsBuiltIn = true,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static CommitteeOfficeHolderType Custom(string name, int order) => new()
    {
        Id = Guid.NewGuid(), Name = name, DisplayOrder = order, IsBuiltIn = false,
        CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    // --- GetActiveAsync ---

    [Fact]
    public async Task GetActiveAsync_DelegatesToRepository_BuiltInsFirstOrdering()
    {
        var types = new List<CommitteeOfficeHolderType>
        {
            BuiltIn("President", 0), BuiltIn("Secretary", 1), BuiltIn("Treasurer", 2),
            Custom("Publicity Officer", 3)
        };
        _repo.GetActiveOrderedAsync(Arg.Any<CancellationToken>()).Returns(types);

        var svc = CreateService();
        var result = await svc.GetActiveAsync(Ct);

        Assert.Same(types, result);
    }

    // --- AddAsync ---

    [Fact]
    public async Task AddAsync_CreatesCustomTitle_AsNotBuiltIn()
    {
        _repo.GetMaxCustomDisplayOrderAsync(Arg.Any<CancellationToken>()).Returns((int?)null);

        var svc = CreateService();
        var result = await svc.AddAsync("Publicity Officer", Ct);

        Assert.Equal("Publicity Officer", result.Name);
        Assert.False(result.IsBuiltIn);
        await _audit.Received(1).LogAsync(nameof(CommitteeOfficeHolderType), result.Id, Enums.AuditAction.Create,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_FirstCustomTitle_GetsDisplayOrder3_NeverAheadOfBuiltIns()
    {
        _repo.GetMaxCustomDisplayOrderAsync(Arg.Any<CancellationToken>()).Returns((int?)null);

        var svc = CreateService();
        var result = await svc.AddAsync("Publicity Officer", Ct);

        Assert.Equal(3, result.DisplayOrder);
    }

    [Fact]
    public async Task AddAsync_SubsequentCustomTitle_GetsNextDisplayOrder()
    {
        _repo.GetMaxCustomDisplayOrderAsync(Arg.Any<CancellationToken>()).Returns(4);

        var svc = CreateService();
        var result = await svc.AddAsync("Webmaster", Ct);

        Assert.Equal(5, result.DisplayOrder);
    }

    [Fact]
    public async Task AddAsync_TrimsName()
    {
        _repo.GetMaxCustomDisplayOrderAsync(Arg.Any<CancellationToken>()).Returns((int?)null);

        var svc = CreateService();
        var result = await svc.AddAsync("  Publicity Officer  ", Ct);

        Assert.Equal("Publicity Officer", result.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task AddAsync_ThrowsValidationException_WhenNameIsBlank(string? name)
    {
        var svc = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => svc.AddAsync(name!, Ct));
    }

    // --- RenameAsync ---

    [Fact]
    public async Task RenameAsync_RenamesCustomTitle()
    {
        var custom = Custom("Publicity Officer", 3);
        _repo.GetByIdAsync(custom.Id, Arg.Any<CancellationToken>()).Returns(custom);

        var svc = CreateService();
        await svc.RenameAsync(custom.Id, "Media Officer", Ct);

        Assert.Equal("Media Officer", custom.Name);
        await _repo.Received(1).UpdateAsync(custom, Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(nameof(CommitteeOfficeHolderType), custom.Id, Enums.AuditAction.Update,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_ThrowsValidationException_WhenTitleIsBuiltIn()
    {
        var president = BuiltIn("President", 0);
        _repo.GetByIdAsync(president.Id, Arg.Any<CancellationToken>()).Returns(president);

        var svc = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => svc.RenameAsync(president.Id, "Chairperson", Ct));
        await _repo.DidNotReceive().UpdateAsync(Arg.Any<CommitteeOfficeHolderType>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_ThrowsEntityNotFoundException_WhenTitleDoesNotExist()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((CommitteeOfficeHolderType?)null);

        var svc = CreateService();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => svc.RenameAsync(Guid.NewGuid(), "New Name", Ct));
    }

    [Fact]
    public async Task RenameAsync_ThrowsValidationException_WhenNewNameIsBlank()
    {
        var custom = Custom("Publicity Officer", 3);
        _repo.GetByIdAsync(custom.Id, Arg.Any<CancellationToken>()).Returns(custom);

        var svc = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => svc.RenameAsync(custom.Id, "  ", Ct));
    }

    // --- ReorderAsync ---

    [Fact]
    public async Task ReorderAsync_AssignsSequentialDisplayOrders_StartingAt3()
    {
        var first = Custom("Webmaster", 3);
        var second = Custom("Publicity Officer", 4);
        _repo.GetByIdAsync(second.Id, Arg.Any<CancellationToken>()).Returns(second);
        _repo.GetByIdAsync(first.Id, Arg.Any<CancellationToken>()).Returns(first);

        var svc = CreateService();
        await svc.ReorderAsync([second.Id, first.Id], Ct);

        Assert.Equal(3, second.DisplayOrder);
        Assert.Equal(4, first.DisplayOrder);
        await _repo.Received(1).UpdateAsync(second, Arg.Any<CancellationToken>());
        await _repo.Received(1).UpdateAsync(first, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReorderAsync_ThrowsValidationException_WhenAnyIdIsBuiltIn()
    {
        var president = BuiltIn("President", 0);
        _repo.GetByIdAsync(president.Id, Arg.Any<CancellationToken>()).Returns(president);

        var svc = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => svc.ReorderAsync([president.Id], Ct));
    }

    [Fact]
    public async Task ReorderAsync_ThrowsEntityNotFoundException_WhenIdDoesNotExist()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((CommitteeOfficeHolderType?)null);

        var svc = CreateService();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => svc.ReorderAsync([Guid.NewGuid()], Ct));
    }

    // --- ArchiveAsync ---

    [Fact]
    public async Task ArchiveAsync_ArchivesCustomTitle()
    {
        var custom = Custom("Publicity Officer", 3);
        _repo.GetByIdAsync(custom.Id, Arg.Any<CancellationToken>()).Returns(custom);

        var svc = CreateService();
        await svc.ArchiveAsync(custom.Id, "coordinator", Ct);

        await _repo.Received(1).ArchiveAsync(custom.Id, "coordinator", Arg.Any<CancellationToken>());
        await _audit.Received(1).LogAsync(nameof(CommitteeOfficeHolderType), custom.Id, Enums.AuditAction.Delete,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveAsync_ThrowsValidationException_WhenTitleIsBuiltIn()
    {
        var treasurer = BuiltIn("Treasurer", 2);
        _repo.GetByIdAsync(treasurer.Id, Arg.Any<CancellationToken>()).Returns(treasurer);

        var svc = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() => svc.ArchiveAsync(treasurer.Id, "coordinator", Ct));
        await _repo.DidNotReceive().ArchiveAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ArchiveAsync_ThrowsEntityNotFoundException_WhenTitleDoesNotExist()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((CommitteeOfficeHolderType?)null);

        var svc = CreateService();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => svc.ArchiveAsync(Guid.NewGuid(), "coordinator", Ct));
    }
}
