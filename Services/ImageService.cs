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
        private readonly string _localSavePath;

        public ImageService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = new HttpClient();
            _cloudflareAccountId = config["CLOUD_FLARE_ACCOUNT_ID"];
            _cloudflareApiToken = config["CLOUD_FLARE_API_TOKEN"];
        }
      
        public async Task<string> UploadImageAsync(Stream imageStream, string fileName)
        {
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

            var response = await _httpClient.SendAsync(request);
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
