namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(B2BucketType bucketType, IFormFile file, string key);
        Task<string> UploadEvidenceAsync(IFormFile file, string key, int retentionDays = 365);
        Task<List<string>> UploadEvidencesAsync(Dictionary<IFormFile, string> filesWithKeys);
        Task<string> GetPresignedUrlAsync(B2BucketType bucketType, string key, double expiryInDays = 7);
        Task<Dictionary<string, string>> GetPresignedUrlsAsync(B2BucketType bucketType, IEnumerable<string> keys, double expiryInDays = 7);
        Task DeleteFileAsync(B2BucketType bucketType, string key);
    }   
}