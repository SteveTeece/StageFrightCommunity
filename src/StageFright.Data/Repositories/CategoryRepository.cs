namespace StageFright.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using StageFright.Core.Entities;
using StageFright.Data.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>Repository implementation for Category entity.</summary>
public class CategoryRepository : BaseRepository<Category>, ICategoryRepository
{
	public CategoryRepository(StageFrightContext context) : base(context) { }

	public async Task<IEnumerable<Category>> GetIncomeCategoriesAsync()
	{
		return await _dbSet
			.Where(c => c.Type == "Income" && !c.IsArchived && !c.IsDeleted)
			.OrderBy(c => c.SortOrder)
			.ToListAsync();
	}

	public async Task<IEnumerable<Category>> GetExpenseCategoriesAsync()
	{
		return await _dbSet
			.Where(c => c.Type == "Expense" && !c.IsArchived && !c.IsDeleted)
			.OrderBy(c => c.SortOrder)
			.ToListAsync();
	}

	public async Task ArchiveAsync(Guid id)
	{
		var category = await GetByIdAsync(id);
		if (category == null)
			throw new InvalidOperationException($"Category with ID {id} not found.");

		category.IsArchived = true;
		await UpdateAsync(category);
	}

	public async Task RestoreAsync(Guid id)
	{
		var category = await GetByIdAsync(id);
		if (category == null)
			throw new InvalidOperationException($"Category with ID {id} not found.");

		category.IsArchived = false;
		await UpdateAsync(category);
	}

	public async Task<bool> ValidateArchivalAsync(Guid id)
	{
		var category = await GetByIdAsync(id);
		if (category == null)
			return false;

		// Check if this category is referenced by any transactions
		var hasTransactions = await _context.Transactions
			.AnyAsync(t => t.Category == category.Name);

		return !hasTransactions;
	}
}
