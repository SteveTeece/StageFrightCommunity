using StageFright.Core.Contracts;
using StageFright.Core.Entities;
using StageFright.Core.Enums;
using StageFright.Core.Exceptions;

namespace StageFright.Core.Modules.Finance;

/// <summary>
/// Records non-member income (raffles, donations, fundraising) directly to the GL.
/// GL pair: Debit Cash (0100) / Credit selected Income category.
/// </summary>
public class IncomeEntryService : IIncomeEntryService
{
    private static readonly Guid CashCategoryId = new("00000000-0000-0000-0000-000000000001");
    private const string CashGLAccount = "0100";

    private readonly ICategoryRepository _categoryRepo;
    private readonly IGLRepository _glRepo;
    private readonly IAuditTrailService _audit;
    private readonly IUnitOfWork _unitOfWork;

    public IncomeEntryService(
        ICategoryRepository categoryRepo,
        IGLRepository glRepo,
        IAuditTrailService audit,
        IUnitOfWork unitOfWork)
    {
        _categoryRepo = categoryRepo;
        _glRepo = glRepo;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<Category>> GetIncomeCategoriesAsync(CancellationToken ct = default)
    {
        var all = await _categoryRepo.GetAllAsync(ct);
        return all
            .Where(c => c.Type == CategoryType.Income && !c.IsSystem)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToList();
    }

    public async Task RecordIncomeAsync(RecordIncomeRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0m)
            throw new ValidationException(
                "Income amount must be greater than zero.", nameof(Transaction), nameof(RecordIncomeAsync));

        var all = await _categoryRepo.GetAllAsync(ct);
        var category = all.FirstOrDefault(c => c.Id == request.CategoryId)
            ?? throw new EntityNotFoundException(
                nameof(Category), request.CategoryId, nameof(RecordIncomeAsync));

        if (category.Type != CategoryType.Income)
            throw new ValidationException(
                "Selected category is not an Income category.", nameof(Category), nameof(RecordIncomeAsync));

        if (category.IsSystem)
            throw new ValidationException(
                "System categories cannot be used for manual income entries.", nameof(Category), nameof(RecordIncomeAsync));

        await _unitOfWork.ExecuteInTransactionAsync(async innerCt =>
        {
            var now = DateTime.UtcNow;
            var description = string.IsNullOrWhiteSpace(request.Description)
                ? $"Income — {category.Name}"
                : request.Description.Trim();

            await _glRepo.AddPairAsync(
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    CategoryId = CashCategoryId,
                    DebitAmount = request.Amount,
                    CreditAmount = 0m,
                    GLAccount = CashGLAccount,
                    MemberId = null,
                    PaymentId = null,
                    FeeId = null,
                    Description = description,
                    CreatedAt = now
                },
                new Transaction
                {
                    Id = Guid.NewGuid(),
                    Date = request.Date,
                    CategoryId = category.Id,
                    DebitAmount = 0m,
                    CreditAmount = request.Amount,
                    GLAccount = category.GLAccount,
                    MemberId = null,
                    PaymentId = null,
                    FeeId = null,
                    Description = description,
                    CreatedAt = now
                },
                innerCt);

            await _audit.LogAsync(
                nameof(Transaction), Guid.Empty, AuditAction.Create,
                oldValue: null,
                newValue: $"Other income {request.Amount:C} to category '{category.Name}' on {request.Date:yyyy-MM-dd}",
                ct: innerCt);

        }, ct);
    }
}
