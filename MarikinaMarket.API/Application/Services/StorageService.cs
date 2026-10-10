using System.Collections.Concurrent;
using System.Diagnostics;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using MarikinaMarket.API.Application.DTOs.Storage;
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
                ContentType = file.ContentType,
                UseChunkEncoding = false,
                DisablePayloadSigning = true
            });

            return key;
        }

        public async Task UploadStreamAsync(B2BucketType bucketType, Stream stream, string key, CancellationToken cancellationToken)
        {
            var (client, bucket) = GetClientAndBucket(bucketType);
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = stream,
                ContentType = "application/octet-stream",
                AutoCloseStream = false,
                UseChunkEncoding = false,
                DisablePayloadSigning = true
            }, cancellationToken);
        }

        public async Task<(Stream Stream, IDisposable Owner)> DownloadStreamAsync(
            B2BucketType bucketType, string key, CancellationToken cancellationToken)
        {
            var (client, bucket) = GetClientAndBucket(bucketType);
            try
            {
                var response = await client.GetObjectAsync(new GetObjectRequest
                {
                    BucketName = bucket,
                    Key = key
                }, cancellationToken);
                return (response.ResponseStream, response);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new RecordNotFoundException("The backup file was not found.");
            }
        }

        public async Task DeleteAllVersionsAsync(B2BucketType bucketType, string key, CancellationToken cancellationToken)
        {
            var (client, bucket) = GetClientAndBucket(bucketType);
            string? keyMarker = null;
            string? versionMarker = null;
            bool hasMore;
            do
            {
                var response = await client.ListVersionsAsync(new ListVersionsRequest
                {
                    BucketName = bucket,
                    Prefix = key,
                    KeyMarker = keyMarker,
                    VersionIdMarker = versionMarker
                }, cancellationToken);
                // Prefix matching alone is insufficient: delete only this backup's exact key.
                foreach (var version in response.Versions ?? [])
                {
                    if (version.Key != key) continue;
                    await client.DeleteObjectAsync(new DeleteObjectRequest
                    {
                        BucketName = bucket,
                        Key = key,
                        VersionId = version.VersionId
                    }, cancellationToken);
                }
                hasMore = response.IsTruncated == true;
                keyMarker = response.NextKeyMarker;
                versionMarker = response.NextVersionIdMarker;
            } while (hasMore);
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
                ObjectLockRetainUntilDate = DateTime.UtcNow.AddDays(retentionDays),
                UseChunkEncoding = false,
                DisablePayloadSigning = true
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

        public async Task<FileMetadata?> GetFileMetadataAsync(B2BucketType bucketType, string key)
        {
            var (s3Client, bucketName) = GetClientAndBucket(bucketType);

            try
            {
                var response = await s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
                {
                    BucketName = bucketName,
                    Key = key
                });

                return new FileMetadata
                {
                    ContentType = response.Headers.ContentType,
                    Size = response.ContentLength
                };
            }
            catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
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
                    MaxErrorRetry = 2,
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
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
                urlMap[key] = await GetPresignedUrlAsync(bucketType, key, expiryInDays);
            }

            return urlMap;
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
