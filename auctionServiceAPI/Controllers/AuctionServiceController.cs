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
    private readonly string? _rabbitMQ;
    public IConnectionFactory ConnectionFactory { get; set; }
    public IMongoDatabase Database { get; set; }
    public IMongoCollection<Auction> AuctionCollection { get; set; }
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
        _config = config;
        _rabbitMQ = config["RabbitMQ"];

        //Retrieves host name and IP address from the current enviroment
        var hostName = System.Net.Dns.GetHostName();
        var ips = System.Net.Dns.GetHostAddresses(hostName);
        var _ipaddress = ips.First().MapToIPv4().ToString();

                //Connects to the database
        var client = new MongoClient(_config["ConnectionString"]);
        _database = client.GetDatabase(_config["DatabaseName"]);
        AuctionCollection = _database.GetCollection<Auction>(_config["CollectionName"]);
        BidCollection = _database.GetCollection<BidDTO>(_config["BidCollection"]);

        //Logs the enviroment variable
        _logger.LogInformation($"RabbitMQ connection is set to : {_rabbitMQ}");
        _logger.LogInformation($"ConnectionString connection is set to : {_config["ConnectionString"]}");
        _logger.LogInformation($"DatabaseName connection is set to : {_config["DatabaseName"]}");
        _logger.LogInformation($"CollectionName connection is set to : {_config["CollectionName"]}");
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
                    $"api/produktkatalog/products/{id}");
        return produkt;
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

    [HttpPut("auction/{id}", Name = "UpdateAuction")]
    public async Task<IActionResult> UpdateAuction(string id, [FromBody] Auction updatedauction)
    {
        var auction = await AuctionCollection.Find(x => x.Id == id).FirstOrDefaultAsync();

        if (auction == null)
        {
            return NotFound();
        }

        updatedauction.Id = auction.Id;

        await AuctionCollection.ReplaceOneAsync(x => x.Id == id, updatedauction);
        return Ok();
    }

    [HttpDelete("auction/{id}", Name = "DeleteAuction")]
    public void DeleteAuction(string id)
    {
        AuctionCollection.DeleteOne(x => x.Id == id);
    }


    /////////////////////////////////////////////////////////BID METHOD//////////////////////////////////////////////////////////////////////////////////////

    [HttpPost("Bid", Name = "SendBid")]
    public void SendBid([FromBody] BidDTO bid)
    {
        var factory = new ConnectionFactory { HostName = _rabbitMQ };

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



