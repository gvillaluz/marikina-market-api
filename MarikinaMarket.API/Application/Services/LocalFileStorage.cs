using MarikinaMarket.API.Application.Interfaces.Services;
using System.Buffers.Text;

namespace MarikinaMarket.API.Application.Services
{
    public class LocalFileStorage : IFileStorage
    {
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LocalFileStorage(IWebHostEnvironment env, IHttpContextAccessor httpContextAccessor)
        {
            _env = env;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string> SaveFileAsync(IFormFile file, string folderName)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File cannot be empty.");

            string webRoot = _env.WebRootPath;
            if (string.IsNullOrEmpty(webRoot))
            {
                webRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            }

            string targetDirectory = Path.Combine(webRoot, folderName);

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            string fileExtension = Path.GetExtension(file.FileName);
            string uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            string fullPath = Path.Combine(targetDirectory, uniqueFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream);
            }

            return $"{folderName}/{uniqueFileName}".Replace("\\", "/");
        }
    }
}