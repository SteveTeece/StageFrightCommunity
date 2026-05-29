namespace StageFright.Maui.Services;

using StageFright.Core.Entities;
using StageFright.Core.Exceptions;
using StageFright.Core.Services;
using StageFright.Data.Repositories;
using StageFright.Data.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Service for finance-related operations including payment recording and balance calculations.</summary>
public class FinanceService : IFinanceService
{
    private readonly IFeeRepository _feeRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly PaymentAllocationService _paymentAllocationService;
    private readonly MemberBalanceService _memberBalanceService;
    private readonly GlTransactionService _glTransactionService;

    public FinanceService(
        IFeeRepository feeRepository,
        IPaymentRepository paymentRepository,
        ITransactionRepository transactionRepository,
        ICategoryRepository categoryRepository,
        IMemberRepository memberRepository,
        ISettingsRepository settingsRepository,
        PaymentAllocationService paymentAllocationService,
        MemberBalanceService memberBalanceService,
        GlTransactionService glTransactionService)
    {
        _feeRepository = feeRepository ?? throw new ArgumentNullException(nameof(feeRepository));
        _paymentRepository = paymentRepository ?? throw new ArgumentNullException(nameof(paymentRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
        _settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
        _paymentAllocationService = paymentAllocationService ?? throw new ArgumentNullException(nameof(paymentAllocationService));
        _memberBalanceService = memberBalanceService ?? throw new ArgumentNullException(nameof(memberBalanceService));
        _glTransactionService = glTransactionService ?? throw new ArgumentNullException(nameof(glTransactionService));
    }

    public async Task<Guid> RecordPaymentAsync(
        DateTime date,
        decimal amount,
        string paymentMethod,
        string paymentType,
        Guid memberId,
        string category,
        string? notes = null)
    {
        // Validate inputs
        if (amount <= 0)
            throw new ValidationException("Payment amount must be positive.");

        if (string.IsNullOrWhiteSpace(paymentMethod))
            throw new ValidationException("Payment method is required.");

        if (string.IsNullOrWhiteSpace(paymentType))
            throw new ValidationException("Payment type is required.");

        if (string.IsNullOrWhiteSpace(category))
            throw new ValidationException("Category is required.");

        var member = await _memberRepository.GetByIdAsync(memberId);
        if (member == null)
            throw new EntityNotFoundException($"Member with ID {memberId} not found.");

        // Create payment record
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Date = date,
            Amount = amount,
            PaymentMethod = paymentMethod,
            PaymentType = paymentType,
            MemberId = memberId,
            Category = category,
            Notes = notes,
            CreatedAt = DateTime.Now
        };

        await _paymentRepository.CreateAsync(payment);

        // Allocate payment to fees using FIFO
        var (allocations, memberCredit) = await _paymentAllocationService.AllocatePaymentAsync(memberId, amount);

        // Create GL transactions for payment
        await _glTransactionService.CreatePaymentTransactionAsync(payment, category);

        return payment.Id;
    }

    public async Task<decimal> GetMemberBalanceAsync(Guid memberId)
    {
        return await _memberBalanceService.GetMemberBalanceAsync(memberId);
    }

    public async Task<IEnumerable<dynamic>> GetCategoriesAsync()
    {
        var categories = await _categoryRepository.GetAllAsync();
        return categories.Select(c => new
        {
            c.Id,
            c.Name,
            c.Type,
            c.GlAccount,
            c.IsArchived
        });
    }

    public async Task<Guid> CreateCategoryAsync(string name, string type)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Category name is required.");

        if (string.IsNullOrWhiteSpace(type))
            throw new ValidationException("Category type is required.");

        if (type != "Income" && type != "Expense")
            throw new ValidationException("Category type must be 'Income' or 'Expense'.");

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            SortOrder = 0,
            IsArchived = false
        };

        await _categoryRepository.CreateAsync(category);
        return category.Id;
    }

    public async Task UpdateCategoryAsync(Guid categoryId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Category name is required.");

        var category = await _categoryRepository.GetByIdAsync(categoryId);
        if (category == null)
            throw new EntityNotFoundException($"Category with ID {categoryId} not found.");

        category.Name = name;
        await _categoryRepository.UpdateAsync(category);
    }

    public async Task ArchiveCategoryAsync(Guid categoryId)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId);
        if (category == null)
            throw new EntityNotFoundException($"Category with ID {categoryId} not found.");

        // Validate archival (prevent if referenced by transactions)
        await _categoryRepository.ValidateArchivalAsync(categoryId);

        category.IsArchived = true;
        await _categoryRepository.UpdateAsync(category);
    }

    public async Task RestoreCategoryAsync(Guid categoryId)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId);
        if (category == null)
            throw new EntityNotFoundException($"Category with ID {categoryId} not found.");

        category.IsArchived = false;
        await _categoryRepository.UpdateAsync(category);
    }

    public async Task<IEnumerable<dynamic>> GetMemberPaymentHistoryAsync(Guid memberId)
    {
        var payments = await _paymentRepository.GetByMemberAsync(memberId);
        return payments.Select(p => new
        {
            p.Id,
            p.Date,
            p.Amount,
            p.PaymentMethod,
            p.PaymentType,
            p.Category,
            p.Notes,
            p.CreatedAt
        });
    }

    public async Task<dynamic> GetPaymentDetailsAsync(Guid paymentId)
    {
        var payment = await _paymentRepository.GetByIdAsync(paymentId);
        if (payment == null)
            throw new EntityNotFoundException($"Payment with ID {paymentId} not found.");

        return new
        {
            payment.Id,
            payment.Date,
            payment.Amount,
            payment.PaymentMethod,
            payment.PaymentType,
            payment.MemberId,
            payment.Category,
            payment.Notes,
            payment.CreatedAt,
            payment.UpdatedAt
        };
    }

    public async Task UpdatePaymentNotesAsync(Guid paymentId, string? notes)
    {
        var payment = await _paymentRepository.GetByIdAsync(paymentId);
        if (payment == null)
            throw new EntityNotFoundException($"Payment with ID {paymentId} not found.");

        await _paymentRepository.UpdateNotesAsync(paymentId, notes);
    }
}
