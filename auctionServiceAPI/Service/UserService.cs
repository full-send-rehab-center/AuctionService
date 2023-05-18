/*
using auctionServiceAPI.DTO;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace UserServiceApi.Services;


public class UserService
{
    private readonly IMongoCollection<UserDTO> _usersCollection;
    private readonly string _connectionString;
    private readonly string _databaseName;
    private readonly string _collectionName;

    public UserService(IOptions<UserDbSettings> usersDbSettings, IConfiguration config)
    {        
        _collectionName = config["CollectionName"];
        _connectionString = config["ConnectionString"];
        _databaseName = config["DatabaseName"];

        var mongoClient = new MongoClient(_connectionString);
        var mongoDatabase = mongoClient.GetDatabase(_databaseName);
        _usersCollection = mongoDatabase.GetCollection<User>(_collectionName);

        // var mongoClient = new MongoClient(usersDbSettings.Value.ConnectionString);
        // var mongoDatabase = mongoClient.GetDatabase(usersDbSettings.Value.DatabaseName);
        // _usersCollection = mongoDatabase.GetCollection<User>(usersDbSettings.Value.CollectionName);
    }

    // Get methods
    // Get all Users
    public async Task<List<User>> GetAsync() =>
        await _usersCollection.Find(s => true).ToListAsync();
    // Get User by ID
    public async Task<User?> GetAsync(string id) =>
        await _usersCollection.Find(x => x.userID == id).FirstOrDefaultAsync();

    // Post methodss
    // Post new User
    public async Task CreateAsync(User newUser) =>
        await _usersCollection.InsertOneAsync(newUser);

    // Update methods
    // Update User
    public async Task UpdateAsync(string id, User updatedUser) =>
        await _usersCollection.ReplaceOneAsync(x => x.userID == id, updatedUser);

    // Delete methods
    // Delete User by ID
    public async Task DeleteAsync(string id) =>
        await _usersCollection.DeleteOneAsync(x => x.userID == id);
}
*/