using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ReviewApi.Services
{
    public class ImageService
    {
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;

        public ImageService(IConfiguration config)
        {
            _storageClient = StorageClient.Create();
            _bucketName = config["GoogleCloud:BucketName"];
        }

        public async Task<string> UploadImageAsync(Stream imageStream, string fileName)
        {
            var objectName = $"{Guid.NewGuid()}_{fileName}";
            await _storageClient.UploadObjectAsync(_bucketName, objectName, null, imageStream);
            return $"https://storage.googleapis.com/{_bucketName}/{objectName}";
        }
    }
}
