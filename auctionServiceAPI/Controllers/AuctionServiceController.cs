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
    public IMongoDatabase Database { get; set; }
    public IMongoCollection<Auction> Collection { get; set; }
    public IMongoCollection<UserDTO> Collection2 { get; set; }
    private List<Auction> _auctions = new List<Auction>();
    private List<UserDTO> _users = new List<UserDTO>();



    public AuctionController(ILogger<AuctionController> logger, IConfiguration config)
    {
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


        //Connects to the database
        var client = new MongoClient(_config["MongoDB:ConnectionString"]);
        _database = client.GetDatabase(_config["MongoDB:Database"]);
        Collection = _database.GetCollection<Auction>(_config["MongoDB:Collection"]);
        Collection2 = _database.GetCollection<UserDTO>(_config["MongoDB:Collection2"]);
    }

    private IMongoCollection<UserDTO> GetUsersCollection(IConfiguration config)
    {
        var ConnectionString = config["MongoDB:ConnectionString"];
        var database = config["MongoDB:Database"];
        var usersCollection = config["MongoDB:Collection2"];

        var client = new MongoClient(ConnectionString);
        var db = client.GetDatabase(database);
        var collection = db.GetCollection<UserDTO>(usersCollection);

        return collection;
    }

    [HttpGet("auctions", Name = "GetAuctions")]
    public List<Auction> GetAuctions()
    {
        var auctionCollection = Collection.Find(new BsonDocument()).ToList();
        auctionCollection.ToJson();
        _logger.LogInformation("GetAuctions method called at {datetime}", DateTime.Now);

        return auctionCollection.ToList();
    }

    [HttpPost("auction", Name = "CreateAuction")]
    public void CreateAuction([FromBody] Auction auction, [FromQuery] string id)
    {

        UserDTO user = Collection2.Find(x => x.Id == id).FirstOrDefault();
        auction.Id = ObjectId.GenerateNewId().ToString();
        auction.StartTime = DateTime.Now;
        auction.EndTime = DateTime.Now.AddDays(2);
        _logger.LogInformation($"**********Product{auction.Id} has been created:**********");

        //Adds the user to the auction
        auction.User = user;

        //Adds the auction to the user
        Collection.InsertOne(auction);
    }
/*
    [HttpPut("auction/{id}", Name = "UpdateAuction")]
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

    [HttpGet("users", Name = "GetUsers")]
    public List<UserDTO> GetUsers()
    {
        var usersCollection = GetUsersCollection(_config);
        var userDocument = usersCollection.Find(new BsonDocument()).ToList();

        _logger.LogInformation("GetUsers method called at {datetime}", DateTime.Now);

        return userDocument.ToList();
    }

    [HttpGet("user/{id}", Name = "GetUserById")]
    public UserDTO GetUserById(string id)
    {
        var usersCollection = GetUsersCollection(_config);
        var userDocument = usersCollection.Find(new BsonDocument()).ToList();

        _logger.LogInformation("GetUsers method called at {datetime}", DateTime.Now);

        return userDocument.ToList().Find(x => x.Id == id);
    }

    [HttpPut("user/{id}", Name = "UpdateUser")]
    public void UpdateUser(string id, [FromBody] UserDTO user)
    {
        var usersCollection = GetUsersCollection(_config);
        var userDocument = usersCollection.Find(new BsonDocument()).ToList();

        var userToUpdate = userDocument.ToList().Find(x => x.Id == id);

        userToUpdate.Username = user.Username;
        userToUpdate.Password = user.Password;
        userToUpdate.Role = user.Role;
        userToUpdate.Address = user.Address;
        userToUpdate.Email = user.Email;
        userToUpdate.Telephone = user.Telephone;

        usersCollection.ReplaceOne(x => x.Id == id, userToUpdate);
    }

    [HttpDelete("user/{id}", Name = "DeleteUser")]
    public void DeleteUser(string id)
    {
        var usersCollection = GetUsersCollection(_config);
        var userDocument = usersCollection.Find(new BsonDocument()).ToList();

        var userToDelete = userDocument.ToList().Find(x => x.Id == id);

        usersCollection.DeleteOne(x => x.Id == id);
    }



    [HttpPost("user", Name = "CreateUser")]
    public void CreateUser([FromBody] UserDTO user)
    {
        var usersCollection = GetUsersCollection(_config);
        user.Id = ObjectId.GenerateNewId().ToString();
        _logger.LogInformation($"**********User{user.Id} has been created:**********");
        usersCollection.InsertOne(user);
    }


    /////////////////////////////////////////////////////////BID METHODS//////////////////////////////////////////////////////////////////////////////////////

    [HttpPost("Bid", Name = "SendBid")]
    public void SendBid(string id, decimal bidAmount, string bidderId, DateTime bidTime, string auctionId)
    {
        var bid = new BidDTO(id, bidAmount, bidderId, bidTime, auctionId)
        {
            Id = id,
            BidAmount = bidAmount,
            BidderId = bidderId,
            BidTime = DateTime.Now,
            AuctionId = auctionId

        };

        //create connection to RabbitMQ server
        var factory = new ConnectionFactory() { HostName = _rabbitMQ };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        _logger.LogInformation($"Connection to RabbitMQ server established at {DateTime.Now}");

        {
            //create queue if it doesn't exist
            channel.QueueDeclare(queue: "bidQueue",
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            //convert bid to JSON
            var body = JsonSerializer.SerializeToUtf8Bytes(bid);
            _logger.LogInformation($"Bid serialized at {DateTime.Now}");

            //send bid to RabbitMQ queue
            channel.BasicPublish(exchange: "",
                                 routingKey: "bidQueue",
                                 basicProperties: null,
                                 body: body);
            _logger.LogInformation($"Bid sent to RabbitMQ queue at {DateTime.Now}");
            Console.WriteLine(" [x] Sent {0}", bid);
/*

            // save bid to cache
            var cacheKey = $"bid_{auctionId}";
            var cacheBid = GetBidFromCache(cacheKey);

            // check if bid amount is greater than previous bid amount
            if (cacheBid == null || bidAmount > cacheBid.BidAmount)
            {
                // create new bid or update existing bid
                SetBidToCache(cacheKey, bid);

                // send new bid to RabbitMQ queue
                channel.BasicPublish(exchange: "", routingKey: "bidQueue", basicProperties: null, body: body);
                _logger.LogInformation($"New bid sent to RabbitMQ queue at {DateTime.Now}");
            }
            else
            {
                // The bid amount is not greater than the previous bid, so no action is taken.
                _logger.LogInformation($"Bid amount is not greater than the previous bid amount. No action taken.");
            }

            Console.WriteLine(" Press [enter] to exit.");

                    //get previous bid from cache

                    //check if bid amount is greater than previous bid amount

                    //create new bid


                    //save bid to cache

                    //send new bid to RabbitMQ queue

                }
                */
                Console.WriteLine(" Press [enter] to exit.");

            }
            
        
    }
}


