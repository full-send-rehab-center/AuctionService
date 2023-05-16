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

namespace auctionServiceAPI.Controllers;

[ApiController]
[Route("[controller]")]
public class AuctionController : ControllerBase
{

    private readonly IConfiguration _config;
    private readonly IMongoDatabase _database;
    private readonly ILogger<AuctionController> _logger;
    private readonly IMongoCollection<Auction> _auctionCollection;
    private readonly string? _docPath;
    private readonly string? _rabbitMQ;

    private List<Auction> _auctions = new List<Auction>();
    private List<BidDTO> _bids = new List<BidDTO>();


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
    }

    [HttpGet(Name = "GetAuctions")]
    public List<Auction> GetAuctions()
    {
        _logger.LogInformation("GetAuctions method called at {datetime}", DateTime.Now);
        var auctionDocument = _auctionCollection.Find(new BsonDocument()).ToList();

        return auctionDocument;
    }

    [HttpGet("{id}", Name = "GetAuctionById")]
    public Auction GetAuctionById(string id)
    {
        _logger.LogInformation($"GetAuctionById method called at {DateTime.Now} with id: {id}");
        var auctionDocument = _auctionCollection.Find<Auction>(auction => auction.AuctionId == id).FirstOrDefault();

        return auctionDocument;
    }

    [HttpPost(Name = "PostAuction")]
    public IActionResult Post([FromBody] Auction auction)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            //Connects to the database
            var client = new MongoClient(_config["MongoDB:ConnectionString"]);
            var database = client.GetDatabase(_config["MongoDB:Database"]);

            var auctionCollection = database.GetCollection<Auction>(_config["MongoDB:Collection"]);

            _auctionCollection.InsertOneAsync(auction);

            //Logs the auction
            _logger.LogInformation($"Auction with id: {auction.AuctionId} and item: {auction.AuctionItem} was created at {DateTime.Now}");

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Something went wrong inside PostAuction action: {ex.Message}");
            return StatusCode(500, "Internal server error");
        }

    }
    //////////////////////lav put auction metode her////////////////////////


    [HttpPost(Name = "PostBid")]
    public void PostBid(string id, decimal bidAmount, string bidder, DateTime bidTime, string auctionId)
    {
        var bid = new BidDTO(id, bidAmount, bidder, bidTime, auctionId)
        {
            Id = id,
            BidAmount = bidAmount,
            Bidder = bidder,
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
            var body = JsonSerializer.SerializeToUtf8Bytes(_bids);
            _logger.LogInformation($"Bid serialized at {DateTime.Now}");

            //send bid to RabbitMQ queue
            channel.BasicPublish(exchange: "",
                                 routingKey: "bidQueue",
                                 basicProperties: null,
                                 body: body);
            _logger.LogInformation($"Bid sent to RabbitMQ queue at {DateTime.Now}");
            Console.WriteLine(" [x] Sent {0}", _bids);


            //get previous bid from cache

            //check if bid amount is greater than previous bid amount

            //create new bid


            //save bid to cache

            //send new bid to RabbitMQ queue

        }
        Console.WriteLine(" Press [enter] to exit.");
    }
}


