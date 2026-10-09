namespace MarikinaMarket.API.Application.Interfaces.Services
{
    using MarikinaMarket.API.Application.DTOs.Storage;

    public interface IStorageService
    {
        Task UploadStreamAsync(B2BucketType bucketType, Stream stream, string key, CancellationToken cancellationToken);
        Task<(Stream Stream, IDisposable Owner)> DownloadStreamAsync(B2BucketType bucketType, string key, CancellationToken cancellationToken);
        Task DeleteAllVersionsAsync(B2BucketType bucketType, string key, CancellationToken cancellationToken);
        Task<string> UploadFileAsync(B2BucketType bucketType, IFormFile file, string key);
        Task<string> UploadEvidenceAsync(IFormFile file, string key, int retentionDays = 365);
        Task<List<string>> UploadEvidencesAsync(Dictionary<IFormFile, string> filesWithKeys);
        Task<string> GetPresignedUrlAsync(B2BucketType bucketType, string key, double expiryInDays = 7);
        Task<Dictionary<string, string>> GetPresignedUrlsAsync(B2BucketType bucketType, IEnumerable<string> keys, double expiryInDays = 7);
        Task<FileMetadata?> GetFileMetadataAsync(B2BucketType bucketType, string key);
        Task DeleteFileAsync(B2BucketType bucketType, string key);
    }   
}
