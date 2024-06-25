using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.cloudflare.com/client/v4/accounts/{_cloudflareAccountId}/images/v1");

            var content = new MultipartFormDataContent
            {
                { new StreamContent(imageStream), "file", fileName }
            };

            request.Content = content;
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cloudflareApiToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            var responseJson = JsonConvert.DeserializeObject<CloudflareImageUploadResponse>(responseBody);

            return responseJson.Result.Variants.First(); // Return the URL of the uploaded image
        }

        private class CloudflareImageUploadResponse
        {
            public CloudflareImageResult Result { get; set; }
        }

        private class CloudflareImageResult
        {
            public List<string> Variants { get; set; }
        }
    }
}
