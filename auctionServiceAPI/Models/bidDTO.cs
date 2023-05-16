using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace auctionServiceAPI.DTO;

public class BidDTO
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    public decimal BidAmount { get; set; }

    public string Bidder { get; set; }
    
    public DateTime BidTime { get; set; }

    public string AuctionId { get; set; }

    public BidDTO(string id, decimal bidAmount, string bidder, DateTime bidTime, string auctionId)
    {
        Id = id;
        BidAmount = bidAmount;
        Bidder = bidder;
        BidTime = bidTime;
        AuctionId = auctionId;
    }

   
}
