using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace ReviewApi.Models
{
    public class Review
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        public string ProductId { get; set; }
        public DateTime Timestamp { get; set; }
        public string ReviewText { get; set; }
        public int Rating { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public List<string> PictureUrls { get; set; } = new List<string>(); // Initialize to avoid null reference
        public int ThumbsUp { get; set; }
        public int ThumbsDown { get; set; }
    }
}
