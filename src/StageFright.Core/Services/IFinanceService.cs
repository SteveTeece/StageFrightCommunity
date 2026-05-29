namespace StageFright.Core.Services;

/// <summary>
/// Service for finance-related operations including payment recording and balance calculations.
/// </summary>
public interface IFinanceService
{
    /// <summary>
    /// Records a payment with GL transaction creation and FIFO allocation.
    /// </summary>
    /// <param name="date">Payment date</param>
    /// <param name="amount">Payment amount</param>
    /// <param name="paymentMethod">Payment method (Cash, Check, Card, Electronic Transfer, Other)</param>
    /// <param name="paymentType">Payment type (Annual, Attendance, Other)</param>
    /// <param name="memberId">Member ID</param>
    /// <param name="category">GL category for the payment</param>
    /// <param name="notes">Optional notes</param>
    /// <returns>The recorded payment ID</returns>
    Task<Guid> RecordPaymentAsync(
        DateTime date,
        decimal amount,
        string paymentMethod,
        string paymentType,
        Guid memberId,
        string category,
        string? notes = null);

    /// <summary>
    /// Calculates the outstanding balance for a member (total unpaid fees).
    /// </summary>
    /// <param name="memberId">Member ID</param>
    /// <returns>Outstanding balance amount</returns>
    Task<decimal> GetMemberBalanceAsync(Guid memberId);

    /// <summary>
    /// Gets all categories.
    /// </summary>
    /// <returns>Collection of categories</returns>
    Task<IEnumerable<dynamic>> GetCategoriesAsync();

    /// <summary>
    /// Creates a new category with GL account assignment.
    /// </summary>
    /// <param name="name">Category name</param>
    /// <param name="type">Category type (Income or Expense)</param>
    /// <returns>The created category ID</returns>
    Task<Guid> CreateCategoryAsync(string name, string type);

    /// <summary>
    /// Updates an existing category.
    /// </summary>
    /// <param name="categoryId">Category ID</param>
    /// <param name="name">Updated name</param>
    /// <returns>Task completion</returns>
    Task UpdateCategoryAsync(Guid categoryId, string name);

    /// <summary>
    /// Archives a category (prevents further use).
    /// </summary>
    /// <param name="categoryId">Category ID</param>
    /// <returns>Task completion</returns>
    Task ArchiveCategoryAsync(Guid categoryId);

    /// <summary>
    /// Restores an archived category.
    /// </summary>
    /// <param name="categoryId">Category ID</param>
    /// <returns>Task completion</returns>
    Task RestoreCategoryAsync(Guid categoryId);

    /// <summary>
    /// Gets all payment records for a member.
    /// </summary>
    /// <param name="memberId">Member ID</param>
    /// <returns>Collection of payment records</returns>
    Task<IEnumerable<dynamic>> GetMemberPaymentHistoryAsync(Guid memberId);

    /// <summary>
    /// Gets payment details by ID.
    /// </summary>
    /// <param name="paymentId">Payment ID</param>
    /// <returns>Payment details</returns>
    Task<dynamic> GetPaymentDetailsAsync(Guid paymentId);

    /// <summary>
    /// Updates payment notes (only editable field).
    /// </summary>
    /// <param name="paymentId">Payment ID</param>
    /// <param name="notes">Updated notes</param>
    /// <returns>Task completion</returns>
    Task UpdatePaymentNotesAsync(Guid paymentId, string? notes);
}
