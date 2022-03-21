namespace SuggestionAppLibrary.DataAccess;
public class MongoUserData : IUserData
{
    private readonly IMongoCollection<UserModel> _users;

    public MongoUserData(IDbConnection db)
    {
        // Creating a copy of UserCollection in _users. We're only referencing it here, so no extra memory usage.
        _users = db.UserCollection;
    }

    /// <summary>
    /// A method to get all users
    /// </summary>
    /// <returns></returns>
    public async Task<List<UserModel>> GetUsersAsync()
    {
        // Returns all records (find all)
        var results = await _users.FindAsync(_ => true);
        return results.ToList();
    }

    /// <summary>
    /// A method to get a single user by ID
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<UserModel> GetUser(string id)
    {
        var results = await _users.FindAsync(u => u.Id == id);
        return results.FirstOrDefault();
    }

    /// <summary>
    /// A method to get a user by Azure B2C identifier
    /// </summary>
    /// <param name="objectId"></param>
    /// <returns></returns>
    public async Task<UserModel> GetUserFromAuthentication(string objectId)
    {
        var results = await _users.FindAsync(u => u.ObjectIdentifier == objectId);
        return results.FirstOrDefault();
    }

    /// <summary>
    /// A method for creating a user
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public Task CreateUser(UserModel user)
    {
        return _users.InsertOneAsync(user);
    }

    /// <summary>
    /// A method for updating a user
    /// </summary>
    /// <param name="user"></param>
    /// <returns></returns>
    public Task UpdateUser(UserModel user)
    {
        var filter = Builders<UserModel>.Filter.Eq("Id", user.Id);
        return _users.ReplaceOneAsync(filter, user, new ReplaceOptions { IsUpsert = true });
    }
}
