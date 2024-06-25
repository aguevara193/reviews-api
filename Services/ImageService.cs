﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace ReviewApi.Services
{
    public class ImageService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private readonly string _cloudflareAccountId;
        private readonly string _cloudflareApiToken;

        public ImageService(IConfiguration config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;
            _cloudflareAccountId = _config["CLOUD_FLARE_ACCOUNT_ID"];
            _cloudflareApiToken = _config["CLOUD_FLARE_API_TOKEN"];
        }

        public async Task<string> UploadImageAsync(Stream imageStream, string fileName)
        {
            try
            {
                var extension = Path.GetExtension(fileName).ToLower();
                var mimeType = MimeTypes.GetMimeType(extension);
               

                var content = new MultipartFormDataContent();
                var fileContent = new StreamContent(imageStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                content.Add(fileContent, "file", fileName);

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri($"https://api.cloudflare.com/client/v4/accounts/{_cloudflareAccountId}/images/v1"),
                    Content = content
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cloudflareApiToken);

              
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var imageUrl = ParseImageUrl(responseContent);
              

                return imageUrl;
            }
            catch (Exception ex)
            {
              
                throw;
            }
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
    }
}
