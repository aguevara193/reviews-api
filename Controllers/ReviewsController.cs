using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ReviewApi.Models;
using ReviewApi.Services;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ReviewApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly ReviewService _reviewService;
        private readonly ImageService _imageService;
        private readonly IDatabase _redisDb;
        private readonly ILogger<ReviewsController> _logger;

        public ReviewsController(ReviewService reviewService, ImageService imageService, IConnectionMultiplexer redis, ILogger<ReviewsController> logger)
        {
            _reviewService = reviewService;
            _imageService = imageService;
            _redisDb = redis.GetDatabase();
            _logger = logger;
        }

        // GET /api/reviews
        [HttpGet]
        public async Task<ActionResult> GetReviews([FromQuery] string productIds, int pageNumber = 1, int pageSize = 10, string sortBy = "newest")
        {
            var productIdList = productIds.Split(',').ToList();
            try
            {
                string cacheKey = $"{string.Join("-", productIdList)}-page-{pageNumber}-size-{pageSize}-sort-{sortBy}";
                var cachedReviews = await _redisDb.StringGetAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedReviews))
                {
                    var cachedResult = JsonSerializer.Deserialize<List<Review>>(cachedReviews);
                    var totalCount = await _reviewService.GetReviewCountAsync(productIdList);
                    var averageRating = await _reviewService.GetCombinedAverageRatingAsync(productIdList);
                    return Ok(new { Reviews = cachedResult, TotalCount = totalCount, AverageRating = averageRating });
                }

                var reviews = await _reviewService.GetReviewsAsync(productIdList, pageNumber, pageSize, sortBy);
                var reviewCount = await _reviewService.GetReviewCountAsync(productIdList);
                var combinedAverageRating = await _reviewService.GetCombinedAverageRatingAsync(productIdList);

                await _redisDb.StringSetAsync(cacheKey, JsonSerializer.Serialize(reviews), TimeSpan.FromMinutes(10));

                return Ok(new { Reviews = reviews, TotalCount = reviewCount, AverageRating = combinedAverageRating });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching reviews: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        [HttpGet("ratings")]
        public async Task<ActionResult> GetRatings([FromQuery] string productIds)
        {
            var productIdList = productIds.Split(',').ToList();
            try
            {
                var result = await _reviewService.GetAverageRatingAndReviewCountAsync(productIdList);
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching ratings: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        // GET /api/reviews/pictures
        [HttpGet("pictures")]
        public async Task<ActionResult<List<string>>> GetAllPictureUrls([FromQuery] string productIds)
        {
            var productIdList = productIds.Split(',').ToList();
            try
            {
                var pictureUrls = await _reviewService.GetAllPictureUrlsAsync(productIdList);
                return Ok(pictureUrls);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching picture URLs: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        // POST /api/reviews
        [HttpPost]
        public async Task<ActionResult<Review>> CreateReview([FromForm] ReviewCreateDto reviewDto)
        {
            try
            {
                var pictureUrls = new List<string>();
                if (reviewDto.Pictures != null && reviewDto.Pictures.Count > 0)
                {
                    foreach (var file in reviewDto.Pictures)
                    {
                        if (file.Length > 0)
                        {
                            using var stream = new MemoryStream();
                            await file.CopyToAsync(stream);
                            var fileName = file.FileName;
                            _logger.LogInformation($"Uploading file {fileName}...");

                            await _imageService.SaveImageLocallyAsync(new MemoryStream(stream.ToArray()), file.FileName);

                          //  pictureUrls.Add(localFilePath);
                            //  var imageUrl = await _imageService.UploadImageAsync(stream, fileName);
                           // pictureUrls.Add(imageUrl);
                        }
                    }
                }

                var review = new Review
                {
                    ProductId = reviewDto.ProductId,
                    Timestamp = reviewDto.Timestamp,
                    ReviewText = reviewDto.ReviewText,
                    Rating = reviewDto.Rating,
                    Name = reviewDto.Name,
                    Email = reviewDto.Email,
                    PictureUrls = pictureUrls,
                    ThumbsUp = 0,
                    ThumbsDown = 0
                };

                await _reviewService.CreateReviewAsync(review);
                await InvalidateCacheAsync(review.ProductId);

                return CreatedAtAction(nameof(GetReviews), new { productIds = review.ProductId }, review);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating review");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        // GET /api/reviews/by-ids
        [HttpGet("by-ids")]
        public async Task<ActionResult<List<Review>>> GetReview([FromQuery] string ids)
        {
            var idList = ids.Split(',').ToList();
            try
            {
                var reviews = await _reviewService.GetReviewsByIdsAsync(idList);

                if (reviews == null || !reviews.Any())
                {
                    return NotFound();
                }

                return Ok(reviews);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching review by ID: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        // PUT /api/reviews/{id}
        [HttpPut("{id:length(24)}")]
        public async Task<IActionResult> UpdateReview(string id, [FromForm] ReviewUpdateDto reviewDto)
        {
            try
            {
                var review = await _reviewService.GetReviewByIdAsync(id);

                if (review == null)
                {
                    return NotFound();
                }

                if (reviewDto.ReviewText != null)
                {
                    review.ReviewText = reviewDto.ReviewText;
                }

                if (reviewDto.Rating.HasValue)
                {
                    review.Rating = reviewDto.Rating.Value;
                }

                if (reviewDto.Pictures != null && reviewDto.Pictures.Count > 0)
                {
                    var pictureUrls = new List<string>();
                    foreach (var file in reviewDto.Pictures)
                    {
                        if (file.Length > 0)
                        {
                            using var stream = new MemoryStream();
                            await file.CopyToAsync(stream);
                            var fileName = file.FileName;
                            var imageUrl = await _imageService.UploadImageAsync(stream, fileName);
                            pictureUrls.Add(imageUrl);
                        }
                    }
                    review.PictureUrls = pictureUrls;
                }

                await _reviewService.UpdateReviewAsync(id, review);
                await InvalidateCacheAsync(review.ProductId);

                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating review: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        // DELETE /api/reviews/{id}
        [HttpDelete("{id:length(24)}")]
        public async Task<IActionResult> DeleteReview(string id)
        {
            try
            {
                var review = await _reviewService.GetReviewByIdAsync(id);

                if (review == null)
                {
                    return NotFound();
                }

                await _reviewService.DeleteReviewAsync(id);
                await InvalidateCacheAsync(review.ProductId);

                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting review: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        // POST /api/reviews/{id}/thumbs-up
        [HttpPost("{id:length(24)}/thumbs-up")]
        public async Task<IActionResult> ThumbsUp(string id)
        {
            try
            {
                var review = await _reviewService.GetReviewByIdAsync(id);
                if (review == null)
                {
                    return NotFound();
                }

                review.ThumbsUp += 1;
                await _reviewService.UpdateReviewAsync(id, review);
                await InvalidateCacheAsync(review.ProductId);

                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding thumbs-up: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        // POST /api/reviews/{id}/thumbs-down
        [HttpPost("{id:length(24)}/thumbs-down")]
        public async Task<IActionResult> ThumbsDown(string id)
        {
            try
            {
                var review = await _reviewService.GetReviewByIdAsync(id);
                if (review == null)
                {
                    return NotFound();
                }

                review.ThumbsDown += 1;
                await _reviewService.UpdateReviewAsync(id, review);
                await InvalidateCacheAsync(review.ProductId);

                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding thumbs-down: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }

        private async Task InvalidateCacheAsync(string productId)
        {
            try
            {
                var server = _redisDb.Multiplexer.GetServer(_redisDb.Multiplexer.GetEndPoints()[0]);
                var keys = server.Keys(pattern: $"{productId}-*");
                foreach (var key in keys)
                {
                    await _redisDb.KeyDeleteAsync(key);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error invalidating cache: {ex.Message}");
            }
        }
    }

    public class ReviewCreateDto
    {
        public string ProductId { get; set; }
        public DateTime Timestamp { get; set; }
        public string ReviewText { get; set; }
        public int Rating { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public List<IFormFile>? Pictures { get; set; }
    }

    public class ReviewUpdateDto
    {
        public string? ReviewText { get; set; }
        public int? Rating { get; set; }
        public List<IFormFile>? Pictures { get; set; }
    }

    public class RatingDto
    {
        public string ProductId { get; set; }
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }
}
