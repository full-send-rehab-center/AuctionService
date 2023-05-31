using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace auctionServiceAPI.DTO
{
    public class UserDTO
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? userID { get; set; }

        public string? username { get; set; }
        public string? password { get; set; }
        public string? salt { get; set; }
        public string? role { get; set; }
        [BsonElement("Name")]
        public string? givenName { get; set; }
        public string? address { get; set; }
        public string? email { get; set; }
        public string? telephone { get; set; }
    }
}
