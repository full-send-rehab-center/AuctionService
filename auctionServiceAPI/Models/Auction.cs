using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace auctionServiceAPI.DTO
{
    public class Auction
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("auctionItem")]
        public string? AuctionItem { get; set; }

        [BsonElement("startingPrice")]
        public decimal StartingPrice { get; set; }

        [BsonElement("currentBid")]
        public decimal CurrentBid { get; set; }

        [BsonElement("startTime")]
        public DateTime StartTime { get; set; }

        [BsonElement("endTime")]
        public DateTime EndTime { get; set; }
/*
        [BsonElement("bid")]
        public BidDTO? Bid { get; set; }
        */
    }
}


//objekt af en User på en auction, så man kan se hvem der sælger
//bud består af Id og BidAmount, så man kan se, hvem der er køber og hvor meget han/hun har budt
//bidDTO skal basically være id og amount 
//http://localhost:5257/auction/postauction