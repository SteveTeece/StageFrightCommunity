namespace StageFright.Core.Services;

using Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>Service for category management.</summary>
public interface ICategoryService
{
	Task<Category> CreateCategoryAsync(Category category);
	Task<Category?> GetCategoryByIdAsync(Guid id);
	Task<IEnumerable<Category>> GetCategoriesAsync(string type);
	Task UpdateCategoryAsync(Category category);
	Task ArchiveCategoryAsync(Guid id);
	Task RestoreCategoryAsync(Guid id);
}
