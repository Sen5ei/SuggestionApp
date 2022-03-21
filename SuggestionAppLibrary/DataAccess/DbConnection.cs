using Microsoft.Extensions.Configuration;

namespace SuggestionAppLibrary.DataAccess;

/// <summary>
/// A method for setting up the connection
/// </summary>
public class DbConnection : IDbConnection
{
    private readonly IConfiguration _config;
    private readonly IMongoDatabase _db;

    // Reference the entry in ConnectionStrings in appsettings.json
    private string _connectionId = "MongoDB";

    // Properties for accessing the collection sets in the database
    public string DbName { get; private set; }
    public string CategoryCollectionName { get; private set; } = "categories";
    public string StatusCollectionName { get; private set; } = "statuses";
    public string UserCollectionName { get; private set; } = "users";
    public string SuggestionCollectionName { get; private set; } = "suggestions";

    // Connection to our tables/collections
    public MongoClient Client { get; private set; }
    public IMongoCollection<CategoryModel> CategoryCollection { get; private set; }
    public IMongoCollection<StatusModel> StatusCollection { get; private set; }
    public IMongoCollection<UserModel> UserCollection { get; private set; }
    public IMongoCollection<SuggestionModel> SuggestionCollection { get; private set; }

    public DbConnection(IConfiguration config)
    {
        _config = config;
        // Configuring new client
        Client = new MongoClient(_config.GetConnectionString(_connectionId));
        // Connection to our database
        DbName = _config["DatabaseName"];
        _db = Client.GetDatabase(DbName);

        // Configuring connection to all 4 collections
        CategoryCollection = _db.GetCollection<CategoryModel>(CategoryCollectionName);
        StatusCollection = _db.GetCollection<StatusModel>(StatusCollectionName);
        UserCollection = _db.GetCollection<UserModel>(UserCollectionName);
        SuggestionCollection = _db.GetCollection<SuggestionModel>(SuggestionCollectionName);
    }
}
