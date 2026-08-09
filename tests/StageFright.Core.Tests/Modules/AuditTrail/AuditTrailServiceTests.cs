using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Modules.AuditTrail;
using StageFright.Core.Tests.Fixtures;

namespace StageFright.Core.Tests.Modules.AuditTrail;

/// <summary>
/// Unit tests for AuditTrailService: LogAsync persistence and PurgeOlderThanAsync's
/// failure-tolerant behaviour (the method callers now reach through IAuditTrailService —
/// see the #275 regression test in StartupSequenceTests for the DI-resolution fix itself).
/// </summary>
public class AuditTrailServiceTests : TestBase
{
    private readonly IAuditTrailRepository _repository = Substitute.For<IAuditTrailRepository>();

    private AuditTrailService CreateService() => new(_repository, NullLogger<AuditTrailService>.Instance);

    [Fact]
    public async Task LogAsync_PersistsEntry_WithExpectedFields()
    {
        var svc = CreateService();
        var entityId = Guid.NewGuid();

        await svc.LogAsync("Member", entityId, AuditAction.Update, "old", "new", "system", Ct);

        await _repository.Received(1).AddAsync(
            Arg.Is<AuditTrailEntry>(e =>
                e.EntityType == "Member" &&
                e.EntityId == entityId &&
                e.Action == AuditAction.Update &&
                e.OldValue == "old" &&
                e.NewValue == "new" &&
                e.UserId == "system"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogAsync_DoesNotCallRepository_WhenSuppressed()
    {
        var svc = CreateService();

        using (AuditTrailSuppressionScope.Begin())
        {
            await svc.LogAsync("Member", Guid.NewGuid(), AuditAction.Create, ct: Ct);
        }

        await _repository.DidNotReceive().AddAsync(Arg.Any<AuditTrailEntry>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeOlderThanAsync_DelegatesToRepository_WithGivenCutoff()
    {
        var svc = CreateService();
        var cutoff = DateTime.UtcNow.AddYears(-3);
        _repository.PurgeOlderThanAsync(cutoff, Arg.Any<CancellationToken>()).Returns(5);

        await svc.PurgeOlderThanAsync(cutoff, Ct);

        await _repository.Received(1).PurgeOlderThanAsync(cutoff, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeOlderThanAsync_DoesNotThrow_WhenRepositoryFails()
    {
        var svc = CreateService();
        _repository.PurgeOlderThanAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new InvalidOperationException("db error"));

        var ex = await Record.ExceptionAsync(() => svc.PurgeOlderThanAsync(DateTime.UtcNow, Ct));

        Assert.Null(ex);
    }

    [Fact]
    public async Task PurgeOlderThanAsync_CompletesSuccessfully_WhenNoEntriesRemoved()
    {
        var svc = CreateService();
        _repository.PurgeOlderThanAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(0);

        var ex = await Record.ExceptionAsync(() => svc.PurgeOlderThanAsync(DateTime.UtcNow, Ct));

        Assert.Null(ex);
    }
}
