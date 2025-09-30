using Google.Cloud.Storage.V1;

namespace Freelancing.Services
{
    public interface IGoogleCloudStorageService
    {
        Task<string> UploadFileAsync(IFormFile file, string folderPath);
        Task<string> UploadFileAsync(byte[] fileBytes, string fileName, string folderPath, string contentType);
        Task<bool> DeleteFileAsync(string fileName);
        Task<Stream> DownloadFileAsync(string fileName);
        Task<bool> FileExistsAsync(string fileName);
        Task<IEnumerable<string>> ListFilesAsync(string folderPath = "");
    }
}