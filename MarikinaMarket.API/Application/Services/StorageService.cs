using System.Collections.Concurrent;
using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using MarikinaMarket.API.Application.Interfaces.Services;

namespace MarikinaMarket.API.Application.Services
{
    public class StorageService : IStorageService
    {
        private readonly IConfiguration _config;
        private readonly string _endpoint;
        private readonly ConcurrentDictionary<B2BucketType, (IAmazonS3 Client, string BucketName)> _clientCache = new();

        public StorageService(IConfiguration config)
        {
            _config = config;
            _endpoint = config["BackBlazeB2:Endpoint"] 
                ?? throw new InvalidOperationException("BackBlazeB2:Endpoint configuration missing.");
        }

        public async Task<string> UploadFileAsync(B2BucketType bucketType, IFormFile file, string key)
        {
            var (s3Client, bucketName) = GetClientAndBucket(bucketType);

            using var stream = file.OpenReadStream();
            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = stream,
                ContentType = file.ContentType
            });

            return key;
        }

        public async Task<string> UploadEvidenceAsync(IFormFile file, string key, int retentionDays = 365)
        {
            var (s3Client, bucketName) = GetClientAndBucket(B2BucketType.Evidence);

            using var stream = file.OpenReadStream();

            var sw = Stopwatch.StartNew();

            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = key,
                InputStream = stream,
                ContentType = file.ContentType,
                ObjectLockMode = ObjectLockMode.Compliance,
                ObjectLockRetainUntilDate = DateTime.UtcNow.AddDays(retentionDays)
            });

            Console.WriteLine(
                $"B2 PutObject: {sw.ElapsedMilliseconds} ms");

            return key;
        }

        public Task<string> GetPresignedUrlAsync(B2BucketType bucketType, string key, double expiryInDays = 7)
        {
            var (s3Client, bucketName) = GetClientAndBucket(bucketType);

            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = key,
                Expires = DateTime.UtcNow.AddDays(expiryInDays)
            };

            string presignedUrl = s3Client.GetPreSignedURL(request);
            return Task.FromResult(presignedUrl);
        }

        private (IAmazonS3 Client, string BucketName) GetClientAndBucket(B2BucketType bucketType)
        {
            return _clientCache.GetOrAdd(bucketType, type =>
            {
                string typeName = type.ToString();

                string keyId = _config[$"BackBlazeB2:{typeName}:KeyId"] 
                    ?? throw new InvalidOperationException($"Missing KeyId for {typeName}");
                string appKey = _config[$"BackBlazeB2:{typeName}:ApplicationKey"] 
                    ?? throw new InvalidOperationException($"Missing ApplicationKey for {typeName}");

                string bucketName = _config[$"BackBlazeB2:Buckets:{typeName}"] 
                    ?? throw new InvalidOperationException($"Missing BucketName for {typeName}");

                var s3Config = new AmazonS3Config
                {
                    ServiceURL = $"https://{_endpoint}",
                    ForcePathStyle = true,
                    AuthenticationRegion = "us-east-005",
                    MaxErrorRetry = 2
                };

                return (new AmazonS3Client(keyId, appKey, s3Config), bucketName);
            });
        }

        public async Task<List<string>> UploadEvidencesAsync(Dictionary<IFormFile, string> filesWithKeys)
        {
            if (filesWithKeys == null || !filesWithKeys.Any()) return [];

            var tasks = filesWithKeys
                .Where(pair => pair.Key.Length > 0)
                .Select(pair => UploadEvidenceAsync(pair.Key, pair.Value));

            string[] keys = await Task.WhenAll(tasks);
            return keys.ToList();
        }

        public async Task<Dictionary<string, string>> GetPresignedUrlsAsync(B2BucketType bucketType, IEnumerable<string> keys, double expiryInDays = 7)
        {
            var urlMap = new Dictionary<string, string>();
            var distinctKeys = keys?.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct() ?? [];

            foreach (var key in distinctKeys)
            {
                urlMap[key] = GetPresignedUrlAsync(bucketType, key, expiryInDays).Result;
            }

            return await Task.FromResult(urlMap);
        }

        public async Task DeleteFileAsync(B2BucketType bucketType, string key)
        {
            var (s3Client, bucketName) = GetClientAndBucket(bucketType);

            await s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = key
            });
        }
    }
}