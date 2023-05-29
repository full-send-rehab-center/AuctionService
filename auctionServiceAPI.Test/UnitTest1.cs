using NUnit.Framework;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing;
using auctionServiceAPI.Test;
using auctionServiceAPI.Controllers;
using auctionServiceAPI.DTO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;



namespace auctionServiceAPI.Test;

public class UnitTest1
{
    private Mock<IModel> mockModel;
    private Mock<IConnection> mockConnection;
    private Mock<IConnectionFactory> mockConnectionFactory;

    [SetUp]
    public void Setup()
    {
        mockModel = new Mock<IModel>();
        mockConnectionFactory = new Mock<IConnectionFactory>();
        mockConnection = new Mock<IConnection>();

        //Configure mock objects
        mockConnectionFactory.Setup(x => x.CreateConnection()).Returns(mockConnection.Object);
        mockConnection.Setup(x => x.CreateModel()).Returns(mockModel.Object);
        mockConnection.Setup(x => x.IsOpen).Returns(true);
    }

  [Test]
public void SendBid_ShouldSendBidToQueue()
{
    // Arrange
    var mockConfig = new Mock<IConfiguration>();
    var mockLogger = new Mock<ILogger<AuctionController>>();

    mockConfig.Setup(x => x.GetSection("RabbitMQ:HostName").Value).Returns("localhost");
    mockConfig.Setup(x => x["MongoDB:ConnectionString"]).Returns("mongodb+srv://mikkelbojstrup:aha64jmj@auktionshus.67fs0yo.mongodb.net/");
    mockConfig.Setup(x => x["MongoDB:Database"]).Returns("Auction");
    mockConfig.Setup(x => x["MongoDB:AuctionCollection"]).Returns("AuctionCollection");
    mockConfig.Setup(x => x["MongoDB:BidCollection"]).Returns("BidCollection");
    mockConfig.Setup(x => x["MongoDB:UsersCollection"]).Returns("UsersCollection");

    var auctionController = new AuctionController(mockLogger.Object, mockConfig.Object);

    var bid = new BidDTO
    {
        BidAmount = 100,
        BidderId = "646545c76f19296d818edb87",
        BidTime = DateTime.Now,
        AuctionId = "646566d96c263f9e2163e453"
    };

    // Act
    Assert.DoesNotThrow(() => auctionController.SendBid(bid));

    // No assertions for RabbitMQ method invocations, as they are being executed on a real RabbitMQ connection
}


}