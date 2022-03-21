using Microsoft.Extensions.Caching.Memory;

namespace SuggestionAppLibrary.DataAccess;
public class MongoSuggestionData : ISuggestionData
{
    private readonly IDbConnection _db;
    private readonly IUserData _userData;
    private readonly IMemoryCache _cache;
    private readonly IMongoCollection<SuggestionModel> _suggestions;
    private const string CacheName = "SuggestionData";

    public MongoSuggestionData(IDbConnection db, IUserData userData, IMemoryCache cache)
    {
        _db = db;
        _userData = userData;
        _cache = cache;
        _suggestions = db.SuggestionCollection;
    }

    /// <summary>
    /// A method for getting all suggestions
    /// </summary>
    /// <returns></returns>
    public async Task<List<SuggestionModel>> GetAllSuggestions()
    {
        var output = _cache.Get<List<SuggestionModel>>(CacheName);
        if (output is null)
        {
            var results = await _suggestions.FindAsync(s => s.Archived == false);
            output = results.ToList();

            // Set a cache time of 1 minute for suggestions
            _cache.Set(CacheName, output, TimeSpan.FromMinutes(1));
        }

        return output;
    }

    /// <summary>
    /// A method for getting all suggestions, including archived suggestions for a particular user
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<List<SuggestionModel>> GetUserSuggestions(string userId)
    {
        var output = _cache.Get<List<SuggestionModel>>(userId);
        if (output is null)
        {
            var results = await _suggestions.FindAsync(s => s.Author.Id == userId);
            output = results.ToList();

            _cache.Set(userId, output, TimeSpan.FromMinutes(1));
        }

        return output;
    }

    /// <summary>
    /// A method for getting all approved suggestions
    /// </summary>
    /// <returns></returns>
    public async Task<List<SuggestionModel>> GetAllApprovedSuggestions()
    {
        var output = await GetAllSuggestions();

        return output.Where(x => x.ApprovedForRelease).ToList();
    }

    /// <summary>
    /// A method to get a suggestion by ID
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<SuggestionModel> GetSuggestion(string id)
    {
        var result = await _suggestions.FindAsync(s => s.Id == id);

        return result.FirstOrDefault();
    }

    /// <summary>
    /// A method for getting all suggestions that are waiting to be approved
    /// </summary>
    /// <returns></returns>
    public async Task<List<SuggestionModel>> GetAllSuggestionsWaitingForApproval()
    {
        var output = await GetAllSuggestions();

        return output.Where(x =>
            x.ApprovedForRelease == false &&
            x.Rejected == false).ToList();
    }

    /// <summary>
    /// A method for updating a suggestion
    /// </summary>
    /// <param name="suggestion"></param>
    /// <returns></returns>
    public async Task UpdateSuggestion(SuggestionModel suggestion)
    {
        await _suggestions.ReplaceOneAsync(s => s.Id == suggestion.Id, suggestion);
        // Destroys the suggestionData cache
        _cache.Remove(CacheName);
    }

    /// <summary>
    /// A method to Upvote a suggestion
    /// </summary>
    /// <param name="suggestionId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task UpvoteSuggestion(string suggestionId, string userId)
    {
        var client = _db.Client;

        // Start a transaction (supported by MongoDB Atlas)
        using var session = await client.StartSessionAsync();

        session.StartTransaction();

        try
        {
            // Get the selected suggestion by ID
            var db = client.GetDatabase(_db.DbName);
            var suggestionsInTransaction = db.GetCollection<SuggestionModel>(_db.SuggestionCollectionName);
            var suggestion = (await suggestionsInTransaction.FindAsync(s => s.Id == suggestionId)).First();

            // Add or remove the Upvote for a suggestion
            bool isUpvote = suggestion.UserVotes.Add(userId);
            if (isUpvote == false)
            {
                suggestion.UserVotes.Remove(userId);
            }

            // After the Upvote is done, update the suggestion by the updated suggestion value
            await suggestionsInTransaction.ReplaceOneAsync(session, s => s.Id == suggestionId, suggestion);

            // Get all users from the database
            var usersInTransaction = db.GetCollection<UserModel>(_db.UserCollectionName);

            // Get the user who Upvoted
            var user = await _userData.GetUser(userId);

            if (isUpvote)
            {
                user.VotedOnSuggestions.Add(new BasicSuggestionModel(suggestion));
            }
            else
            {
                var suggestionToRemove = user.VotedOnSuggestions.Where(s => s.Id == suggestionId).First();
                user.VotedOnSuggestions.Remove(suggestionToRemove);
            }

            // After the Upvote is done, update the user by updated user value
            await usersInTransaction.ReplaceOneAsync(session, u => u.Id == userId, user);

            await session.CommitTransactionAsync();

            // Remove from cache since it is an update
            _cache.Remove(CacheName);
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync();
            throw;
        }
    }

    /// <summary>
    /// A method for creating a suggestion
    /// </summary>
    /// <param name="suggestion"></param>
    /// <returns></returns>
    public async Task CreateSuggestion(SuggestionModel suggestion)
    {
        var client = _db.Client;

        // Start a transaction, as since a user is creating the suggestion, we also need to update the user account
        using var session = await client.StartSessionAsync();

        session.StartTransaction();

        try
        {
            var db = client.GetDatabase(_db.DbName);
            // Insert the new suggestion into the suggestions table
            var suggestionsInTransaction = db.GetCollection<SuggestionModel>(_db.SuggestionCollectionName);
            await suggestionsInTransaction.InsertOneAsync(session, suggestion);

            // Update the user
            var usersInTransaction = db.GetCollection<UserModel>(_db.UserCollectionName);
            var user = await _userData.GetUser(suggestion.Author.Id);
            user.AuthoredSuggestions.Add(new BasicSuggestionModel(suggestion));
            await usersInTransaction.ReplaceOneAsync(session, u => u.Id == user.Id, user);

            await session.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync();
            throw;
        }
    }
}
