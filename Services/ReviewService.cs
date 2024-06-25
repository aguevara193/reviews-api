using MongoDB.Driver;
using ReviewApi.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReviewApi.Services
{
    public class ReviewService
    {
        private readonly IMongoCollection<Review> _reviews;

        public ReviewService(IConfiguration config)
        {
            var client = new MongoClient(config.GetConnectionString("ReviewDb"));
            var database = client.GetDatabase("ReviewDb");
            _reviews = database.GetCollection<Review>("Reviews");

            // Ensure indexes
            var indexKeys = Builders<Review>.IndexKeys;
            _reviews.Indexes.CreateOne(new CreateIndexModel<Review>(indexKeys.Ascending(r => r.ProductId)));
            _reviews.Indexes.CreateOne(new CreateIndexModel<Review>(indexKeys.Descending(r => r.Timestamp)));
            _reviews.Indexes.CreateOne(new CreateIndexModel<Review>(indexKeys.Ascending(r => r.Rating)));
            _reviews.Indexes.CreateOne(new CreateIndexModel<Review>(indexKeys.Descending(r => r.ThumbsUp)));
        }

        public async Task<List<Review>> GetReviewsByProductIdAsync(string productId)
        {
            return await _reviews.Find(review => review.ProductId == productId).ToListAsync();
        }

        public async Task<List<Review>> GetReviewsAsync(List<string> productIds, int pageNumber, int pageSize, string sortBy)
        {
            var filter = Builders<Review>.Filter.In(r => r.ProductId, productIds);
            SortDefinition<Review> sort = null;

            switch (sortBy)
            {
                case "newest":
                    sort = Builders<Review>.Sort.Descending(r => r.Timestamp);
                    break;
                case "oldest":
                    sort = Builders<Review>.Sort.Ascending(r => r.Timestamp);
                    break;
                case "mostHelpful":
                    sort = Builders<Review>.Sort.Descending(r => r.ThumbsUp);
                    break;
                case "reviewWithPhotos":
                    var pipeline = _reviews.Aggregate()
                        .Match(filter)
                        .Project(r => new
                        {
                            Review = r,
                            HasPictures = r.PictureUrls != null && r.PictureUrls.Count > 0
                        })
                        .SortByDescending(r => r.HasPictures)
                        .ThenByDescending(r => r.Review.Timestamp)
                        .Skip((pageNumber - 1) * pageSize)
                        .Limit(pageSize)
                        .Project(r => r.Review);

                    return await pipeline.ToListAsync();
                default:
                    sort = Builders<Review>.Sort.Descending(r => r.Timestamp);
                    break;
            }

            return await _reviews.Find(filter)
                                 .Sort(sort)
                                 .Skip((pageNumber - 1) * pageSize)
                                 .Limit(pageSize)
                                 .ToListAsync();
        }

        public async Task<int> GetReviewCountAsync(List<string> productIds)
        {
            var filter = Builders<Review>.Filter.In(r => r.ProductId, productIds);
            return (int)await _reviews.CountDocumentsAsync(filter);
        }

        public async Task<double> GetCombinedAverageRatingAsync(List<string> productIds)
        {
            var filter = Builders<Review>.Filter.In(r => r.ProductId, productIds);
            var average = await _reviews.Aggregate()
                                        .Match(filter)
                                        .Group(r => r.ProductId, g => new
                                        {
                                            AverageRating = g.Average(r => r.Rating)
                                        })
                                        .ToListAsync();

            return average.Select(a => a.AverageRating).Average();
        }

        public async Task<List<RatingDto>> GetAverageRatingAndReviewCountAsync(List<string> productIds)
        {
            var filter = Builders<Review>.Filter.In(r => r.ProductId, productIds);
            var group = _reviews.Aggregate()
                                .Match(filter)
                                .Group(r => r.ProductId, g => new RatingDto
                                {
                                    ProductId = g.Key,
                                    AverageRating = g.Average(r => r.Rating),
                                    ReviewCount = g.Count()
                                });

            return await group.ToListAsync();
        }

        public async Task<List<string>> GetAllPictureUrlsAsync(List<string> productIds)
        {
            var filter = Builders<Review>.Filter.In(r => r.ProductId, productIds);
            var reviews = await _reviews.Find(filter).ToListAsync();
            return reviews.SelectMany(r => r.PictureUrls ?? new List<string>()).ToList();
        }

        public async Task<Review> CreateReviewAsync(Review review)
        {
            await _reviews.InsertOneAsync(review);
            return review;
        }

        public async Task<Review> GetReviewByIdAsync(string id)
        {
            return await _reviews.Find(review => review.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Review>> GetReviewsByIdsAsync(List<string> ids)
        {
            var filter = Builders<Review>.Filter.In(r => r.Id, ids);
            return await _reviews.Find(filter).ToListAsync();
        }

        public async Task<bool> UpdateReviewAsync(string id, Review updatedReview)
        {
            var result = await _reviews.ReplaceOneAsync(review => review.Id == id, updatedReview);
            return result.IsAcknowledged && result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteReviewAsync(string id)
        {
            var result = await _reviews.DeleteOneAsync(review => review.Id == id);
            return result.IsAcknowledged && result.DeletedCount > 0;
        }
    }

    public class RatingDto
    {
        public string ProductId { get; set; }
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }
}
