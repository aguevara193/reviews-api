using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ReviewApi.Services
{
    public class ImageService
    {
        private readonly HttpClient _httpClient;
        private readonly string _cloudflareAccountId;
        private readonly string _cloudflareApiToken;
        private readonly ILogger<ImageService> _logger;
        private readonly string _localSavePath;

        public ImageService(HttpClient httpClient, IConfiguration config, ILogger<ImageService> logger)
        {
            _httpClient = httpClient;
            _cloudflareAccountId = config["CLOUD_FLARE_ACCOUNT_ID"];
            _cloudflareApiToken = config["CLOUD_FLARE_API_TOKEN"];
            _localSavePath = config["LocalSavePath"];
            _logger = logger;
        }
        public async Task SaveImageLocallyAsync(Stream imageStream, string fileName)
        {
            try
            {
                var filePath = Path.Combine("/root/images", fileName);

                _logger.LogInformation($"Saving file locally to {filePath}");

                Directory.CreateDirectory("/root/images");

                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                await imageStream.CopyToAsync(fileStream);

                _logger.LogInformation("File saved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving image locally");
                throw;
            }
        }
        public async Task<string> UploadImageAsync(Stream imageStream, string fileName)
        {
            using var httpClient = new HttpClient();
            var extension = Path.GetExtension(fileName).ToLower();
            var mimeType = GetMimeType(extension);

            using var content = new MultipartFormDataContent();
            using var fileStreamContent = new StreamContent(imageStream);
            fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            content.Add(fileStreamContent, "file", fileName);

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri($"https://api.cloudflare.com/client/v4/accounts/{_cloudflareAccountId}/images/v1"),
                Content = content,
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cloudflareApiToken);

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return ParseImageUrl(responseContent);
        }

        private string ParseImageUrl(string responseContent)
        {
            using var jsonDoc = JsonDocument.Parse(responseContent);
            if (jsonDoc.RootElement.TryGetProperty("result", out var resultElement) &&
                resultElement.TryGetProperty("variants", out var variantsElement) &&
                variantsElement.GetArrayLength() > 0)
            {
                return variantsElement[0].GetString();
            }
            throw new InvalidOperationException("Failed to parse image URL from Cloudflare response.");
        }

        private string GetMimeType(string extension)
        {
            return extension switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".avif" => "image/avif",
                _ => throw new NotSupportedException($"Extension {extension} is not supported"),
            };
        }

        
    }
}
