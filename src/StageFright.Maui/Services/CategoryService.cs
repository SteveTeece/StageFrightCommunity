namespace StageFright.Maui.Services;

using Entities;
using Exceptions;
using StageFright.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Service for category management with GL account integration.</summary>
public class CategoryService : ICategoryService
{
	private readonly ICategoryRepository _categoryRepository;
	private readonly GLAccountAssignmentService _glAccountService;

	public CategoryService(
		ICategoryRepository categoryRepository,
		GLAccountAssignmentService glAccountService)
	{
		_categoryRepository = categoryRepository;
		_glAccountService = glAccountService;
	}

	public async Task<Category> CreateCategoryAsync(Category category)
	{
		if (string.IsNullOrWhiteSpace(category.Name))
			throw new ValidationException("Category name is required.");

		if (string.IsNullOrWhiteSpace(category.Type))
			throw new ValidationException("Category type is required.");

		// Assign GL account before persisting
		category.GLAccount = _glAccountService.AssignGLAccount(category.Type);
		category.IsDeleted = false;

		await _categoryRepository.CreateAsync(category);
		return category;
	}

	public async Task<Category?> GetCategoryByIdAsync(Guid id)
	{
		return await _categoryRepository.GetByIdAsync(id);
	}

	public async Task<IEnumerable<Category>> GetCategoriesAsync(string type)
	{
		if (string.IsNullOrWhiteSpace(type))
			return await _categoryRepository.GetAllAsync();

		return await _categoryRepository.GetByTypeAsync(type);
	}

	public async Task UpdateCategoryAsync(Category category)
	{
		if (category.Id == Guid.Empty)
			throw new ValidationException("Category ID is required.");

		var existing = await _categoryRepository.GetByIdAsync(category.Id);
		if (existing == null)
			throw new EntityNotFoundException($"Category with ID {category.Id} not found.");

		// Preserve immutable fields
		category.GLAccount = existing.GLAccount;
		category.CreatedAt = existing.CreatedAt;
		category.IsDeleted = existing.IsDeleted;
		category.DeletedAt = existing.DeletedAt;
		category.DeletedBy = existing.DeletedBy;

		await _categoryRepository.UpdateAsync(category);
	}

	public async Task ArchiveCategoryAsync(Guid id)
	{
		var category = await _categoryRepository.GetByIdAsync(id);
		if (category == null)
			throw new EntityNotFoundException($"Category with ID {id} not found.");

		if (category.IsDeleted)
			return; // Already archived

		category.IsDeleted = true;
		category.DeletedAt = DateTime.UtcNow;

		await _categoryRepository.UpdateAsync(category);
	}

	public async Task RestoreCategoryAsync(Guid id)
	{
		var category = await _categoryRepository.GetByIdAsync(id);
		if (category == null)
			throw new EntityNotFoundException($"Category with ID {id} not found.");

		if (!category.IsDeleted)
			return; // Already active

		category.IsDeleted = false;
		category.DeletedAt = null;
		category.DeletedBy = null;

		await _categoryRepository.UpdateAsync(category);
	}
}
