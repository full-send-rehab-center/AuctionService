using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("[controller]")]
public class bidController : ControllerBase
{
    private readonly ILogger<bidController> _logger;
    private readonly string? _docPath;
    private readonly string? _rabbitMQ;

    public bidController(ILogger<bidController> logger, IConfiguration config)
    {
        //Takes enviroment variable and sets it to the logger
        _logger = logger;
        _docPath = config["DocPath"];
        _rabbitMQ = config["RabbitMQ"];
        
        //Logs the enviroment variable
        _logger.LogInformation($"File path is set to : {_docPath}");
        _logger.LogInformation($"RabbitMQ connection is set to : {_rabbitMQ}");
    }

    [HttpPost(Name = "PostBid")]
    public IActionResult Post([FromBody] bidDTO bid)
    {
        _logger.LogInformation($"Bid received for item {bid.bidItemId} for amount {bid.bidAmount}");

        //create connection to RabbitMQ server
        var factory = new ConnectionFactory() { HostName = _rabbitMQ };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        //create queue if it doesn't exist
        channel.QueueDeclare(queue: "bidQueue",
                             durable: false,
                             exclusive: false,
                             autoDelete: false,
                             arguments: null);





        //get previous bid from cache





        //check if bid amount is greater than previous bid amount





        //create new bid
        var newBid = new bidDTO
        {
            bidId = bid.bidId,
            bidAmount = bid.bidAmount,
            bidItemId = bid.bidItemId,
            bidUserId = bid.bidUserId,
            bidDate = DateTime.Now,
            bidderUserName = bid.bidderUserName
        };

        //save bid to cache

        //send new bid to RabbitMQ queue
        var message = JsonSerializer.Serialize(newBid);
        var body = Encoding.UTF8.GetBytes(message);
        channel.BasicPublish(exchange: "",
                             routingKey: "bidQueue",
                             basicProperties: null,
                             body: body);
        
        return Ok();
    
    }
}
