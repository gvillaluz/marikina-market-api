namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IFileStorage
    {
        Task<string> SaveFileAsync(IFormFile file, string folderName);
    }
}
