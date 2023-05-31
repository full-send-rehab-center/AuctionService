using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using auctionServiceAPI.DTO;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Core.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Net.Http;


namespace auctionServiceAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class AuctionController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IMongoDatabase _database;
    private readonly ILogger<AuctionController> _logger;
    private readonly string? _docPath;
    private readonly string? _rabbitMQ;
    public IConnectionFactory ConnectionFactory { get; set; }
    public IMongoDatabase Database { get; set; }
    public IMongoCollection<Auction> AuctionCollection { get; set; }
    public IMongoCollection<UserDTO> UsersCollection { get; set; }
    public IMongoCollection<BidDTO> BidCollection { get; set; }
    private List<Auction> _auctions = new List<Auction>();
    private List<UserDTO> _users = new List<UserDTO>();

    private readonly HttpClient _httpClientUser;
    private readonly HttpClient _httpClientProduct;
    private readonly string? _baseURIUser;
    private readonly string? _baseURIProduct;
 
    public AuctionController(ILogger<AuctionController> logger, IConfiguration config, HttpClient httpClientUser, HttpClient httpClientProduct)
    {
        // Initiates HttpClient for UserService
        _baseURIUser = config["BaseURIUser"];
        _httpClientUser = httpClientUser;
        // Initiates HttpClient for ProductService
        _baseURIProduct = config["BaseURIProduct"];
        _httpClientProduct = httpClientProduct;


        //Takes enviroment variable and sets it to the logger
        _logger = logger;
        _docPath = config["DocPath"];
        _rabbitMQ = config["RabbitMQ"];
        _config = config;

        //Retrieves host name and IP address from the current enviroment
        var hostName = System.Net.Dns.GetHostName();
        var ips = System.Net.Dns.GetHostAddresses(hostName);
        var _ipaddress = ips.First().MapToIPv4().ToString();

        //Logs the enviroment variable
        _logger.LogInformation($"File path is set to : {_docPath}");
        _logger.LogInformation($"RabbitMQ connection is set to : {_rabbitMQ}");

        //Sætter baseuri for produktservice og userservice
        

        //Connects to the database
        var client = new MongoClient(_config["ConnectionString"]);
        _database = client.GetDatabase(_config["DatabaseName"]);
        AuctionCollection = _database.GetCollection<Auction>(_config["CollectionName"]);
        UsersCollection = _database.GetCollection<UserDTO>(_config["MongoDB:UsersCollection"]);
        BidCollection = _database.GetCollection<BidDTO>(_config["MongoDB:BidCollection"]);
    }

    // HttpClient that pulls a user by ID from UserService
    [HttpGet("user", Name = "GetUsersAsync")]
    public async Task<UserDTO> GetUsersAsync(string id)
    {
        _httpClientUser.BaseAddress = new Uri(_baseURIUser);
        var user = await _httpClientUser.GetFromJsonAsync<UserDTO>(
                    $"api/brugerservice/{id}");
        _logger.LogInformation($"Recieved user with values: {user.userID} {user.username} {user.password} {user.salt} {user.role} {user.givenName} {user.address} {user.email} {user.telephone}");
        return user;
    }

    // HttpClient that pulls a product by ID from ProductCatalog
    [HttpGet("produkt", Name = "GetProduktAsync")]
    public async Task<ProduktKatalog> GetProduktAsync(string id)
    {
        _httpClientProduct.BaseAddress = new Uri(_baseURIProduct);
        var produkt = await _httpClientProduct.GetFromJsonAsync<ProduktKatalog>(
                    $"api/produktkatalog/category/{id}");
        return produkt;
    }


    private IMongoCollection<UserDTO> GetUsersCollection(IConfiguration config)
    {
        var ConnectionString = config["MongoDB:ConnectionString"];
        var database = config["MongoDB:Database"];
        var usersCollection = config["MongoDB:UsersCollection"];

        var client = new MongoClient(ConnectionString);
        var db = client.GetDatabase(database);
        var collection = db.GetCollection<UserDTO>(usersCollection);

        return collection;
    }

    [HttpGet("auctions", Name = "GetAuctions")]
    public List<Auction> GetAuctions()
    {
        var auctionCollection = AuctionCollection.Find(new BsonDocument()).ToList();
        auctionCollection.ToJson();
        _logger.LogInformation("GetAuctions method called at {datetime}", DateTime.Now);

        return auctionCollection.ToList();
    }

     [HttpPost("auction", Name = "CreateAuction")]
    public async Task<IActionResult> CreateAuction([FromBody] Auction auction, [FromQuery] string userid, [FromQuery] string produktid)
    {
        // UserDTO user = UsersCollection.Find(x => x.UserId == id).FirstOrDefault();
        UserDTO user = await GetUsersAsync(userid);
        ProduktKatalog produkt = await GetProduktAsync(produktid);
        auction.StartTime = DateTime.Now;
        auction.EndTime = DateTime.Now.AddDays(2);
        _logger.LogInformation($"**********Product{auction.Id} has been created:**********");

        //Adds the user to the auction
        auction.User = user;
        auction.AuctionItem = produkt;

        //Adds the auction to the user
        AuctionCollection.InsertOne(auction);
        return Ok();
    }
    /*
        [HttpPut("auction/{id}", Name = "UpdateAuction")]a
        public void UpdateAuction(string id, [FromBody] Auction auction)
        {
            var auctionToUpdate = Collection.Find(x => x.Id == id).FirstOrDefault();

            auctionToUpdate.AuctionItem = auction.AuctionItem;
            auctionToUpdate.StartingPrice = auction.StartingPrice;
            auctionToUpdate.CurrentBid = auction.CurrentBid;
            auctionToUpdate.StartTime = auction.StartTime;
            auctionToUpdate.EndTime = auction.EndTime;

            Collection.ReplaceOne(x => x.Id == id, auctionToUpdate);
        }
    */
    //___________________________________________________________________________________________________________________________________________________________//
    // User methods

    // [HttpGet("users", Name = "GetUsers")]
    // public List<UserDTO> GetUsers()
    // {
    //     var usersCollection = GetUsersCollection(_config);
    //     var userDocument = usersCollection.Find(new BsonDocument()).ToList();

    //     _logger.LogInformation("GetUsers method called at {datetime}", DateTime.Now);

    //     return userDocument.ToList();
    // }

    // [HttpGet("user/{id}", Name = "GetUserById")]
    // public UserDTO GetUserById(string id)
    // {
    //     var usersCollection = GetUsersCollection(_config);
    //     var userDocument = usersCollection.Find(new BsonDocument()).ToList();

    //     _logger.LogInformation("GetUsers method called at {datetime}", DateTime.Now);

    //     return userDocument.ToList().Find(x => x.UserId == id);
    // }

    // [HttpPut("user/{id}", Name = "UpdateUser")]
    // public void UpdateUser(string id, [FromBody] UserDTO user)
    // {
    //     var usersCollection = GetUsersCollection(_config);
    //     var userDocument = usersCollection.Find(new BsonDocument()).ToList();

    //     var userToUpdate = userDocument.ToList().Find(x => x.UserId == id);

    //     userToUpdate.Username = user.Username;
    //     userToUpdate.Password = user.Password;
    //     userToUpdate.Role = user.Role;
    //     userToUpdate.Address = user.Address;
    //     userToUpdate.Email = user.Email;
    //     userToUpdate.Telephone = user.Telephone;

    //     usersCollection.ReplaceOne(x => x.UserId == id, userToUpdate);
    // }

    // [HttpDelete("user/{id}", Name = "DeleteUser")]
    // public void DeleteUser(string id)
    // {
    //     var usersCollection = GetUsersCollection(_config);
    //     var userDocument = usersCollection.Find(new BsonDocument()).ToList();

    //     var userToDelete = userDocument.ToList().Find(x => x.UserId == id);

    //     usersCollection.DeleteOne(x => x.UserId == id);
    // }



    [HttpPost("user", Name = "CreateUser")]
    public void CreateUser([FromBody] UserDTO user)
    {
        var usersCollection = GetUsersCollection(_config);
        user.userID = ObjectId.GenerateNewId().ToString();
        usersCollection.InsertOne(user);
        _logger.LogInformation($"**********User{user.userID} has been created:**********");
    }


    /////////////////////////////////////////////////////////BID METHODS//////////////////////////////////////////////////////////////////////////////////////
    //Gets the user id from the user collection
    private string GetUserId(string bidderId)
    {
        var user = UsersCollection.Find(x => x.userID == bidderId).FirstOrDefault();
        return user.userID;
    }

    //Gets the auction id from the auction collection
    private string GetAuctionId(string auctionId)
    {
        var auction = AuctionCollection.Find(x => x.Id == auctionId).FirstOrDefault();
        return auction.Id;
    }



    [HttpPost("Bid", Name = "SendBid")]
    public void SendBid([FromBody] BidDTO bid)
    {
        //var factory = new RabbitMQ.Client.ConnectionFactory() { HostName = "localhost" };
        // var rabbitMQConnectionString = _config["RabbitMQ:ConnectionString"];
        var rabbitMQConnectionString = _config["RabbitMQ"];
        var factory = new RabbitMQ.Client.ConnectionFactory() { Uri = new Uri(rabbitMQConnectionString) };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        {
            if (!connection.IsOpen)
            {
                _logger.LogError($"Failed to establish connection to RabbitMQ server at {DateTime.Now}");
                return;
            }
            else
            {
                _logger.LogInformation($"Connection to RabbitMQ server established at {DateTime.Now}");

                channel.ExchangeDeclare(exchange: "bidExchange", type: ExchangeType.Topic);
                channel.QueueDeclare(queue: "bidQueue",
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(bid));
                _logger.LogInformation($"Bid serialized at {DateTime.Now}");

                var properties = channel.CreateBasicProperties();
                properties.Persistent = true; //Makes sure the message is not lost if the RabbitMQ server crashes

                // Enable publisher confirms
                channel.ConfirmSelect();

                // Event handler for successful message delivery
                channel.BasicAcks += (sender, eventArgs) =>
                {
                    if (eventArgs.Multiple)
                    {
                        _logger.LogInformation("Multiple messages were successfully delivered to RabbitMQ queue");
                    }
                    else
                    {
                        _logger.LogInformation("Message was successfully delivered to RabbitMQ queue");
                    }
                };

                // Event handler for failed message delivery
                channel.BasicNacks += (sender, eventArgs) =>
                {
                    _logger.LogWarning("Failed to deliver message to RabbitMQ queue");
                };

                channel.BasicPublish(exchange: "bidExchange",
                                     routingKey: "bid",
                                     basicProperties: properties,
                                     body: body);

                // Wait until all confirms are received (or timeout after 5 seconds)
                if (!channel.WaitForConfirms(TimeSpan.FromSeconds(5)))
                {
                    _logger.LogWarning("Timeout occurred while waiting for confirms");
                }

                _logger.LogInformation($"Bid sent to RabbitMQ queue at {DateTime.Now}");
                Console.WriteLine(" [x] Sent {0}", bid);
            }
        }
    }


}



