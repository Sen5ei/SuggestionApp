using Microsoft.Extensions.Caching.Memory;

namespace SuggestionAppLibrary.DataAccess;
public class MongoCategoryData : ICategoryData
{
    private readonly IMemoryCache _cache;
    private readonly IMongoCollection<CategoryModel> _categories;
    private const string CacheName = "CategoryData";

    public MongoCategoryData(IDbConnection db, IMemoryCache cache)
    {
        _cache = cache;
        _categories = db.CategoryCollection;
    }

    /// <summary>
    /// A method to get all categories
    /// </summary>
    /// <returns></returns>
    public async Task<List<CategoryModel>> GetAllCategories()
    {
        // Get data from cache
        var output = _cache.Get<List<CategoryModel>>(CacheName);
        if (output is null)
        {
            var results = await _categories.FindAsync(_ => true);
            output = results.ToList();

            // Put value in cache (data will be cached for 1 day)
            _cache.Set(CacheName, output, TimeSpan.FromDays(1));
        }

        return output;
    }

    /// <summary>
    /// A method to create a category
    /// </summary>
    /// <param name="category"></param>
    /// <returns></returns>
    public Task CreateCategory(CategoryModel category)
    {
        return _categories.InsertOneAsync(category);
    }
}
