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
        Task<string> GenerateSignedUrlAsync(string objectName, TimeSpan expiration); // ADD THIS
    }
}