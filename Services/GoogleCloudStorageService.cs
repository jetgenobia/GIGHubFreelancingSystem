using Google;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using System.Text.Json;

namespace Freelancing.Services
{
    public class GoogleCloudStorageService : IGoogleCloudStorageService
    {
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;
        private readonly ILogger<GoogleCloudStorageService> _logger;

        public GoogleCloudStorageService(IConfiguration configuration, ILogger<GoogleCloudStorageService> logger)
        {
            _logger = logger;
            _bucketName = configuration["GoogleCloud:BucketName"] ??
                         Environment.GetEnvironmentVariable("GOOGLE_CLOUD_BUCKET_NAME") ??
                         throw new InvalidOperationException("Google Cloud bucket name not configured");

            try
            {
                // Try to get credentials from environment variable (JSON string)
                var credentialsJson = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_CREDENTIALS");

                if (!string.IsNullOrEmpty(credentialsJson))
                {
                    // Parse credentials from JSON string (for Railway deployment)
                    var credential = GoogleCredential.FromJson(credentialsJson);
                    _storageClient = StorageClient.Create(credential);
                }
                else
                {
                    // Use default credentials (for local development with gcloud CLI)
                    _storageClient = StorageClient.Create();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Google Cloud Storage client");
                throw;
            }
        }

        public async Task<string> UploadFileAsync(IFormFile file, string folderPath)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is null or empty", nameof(file));

            var fileName = GenerateUniqueFileName(file.FileName, folderPath);

            try
            {
                using var stream = file.OpenReadStream();

                var objectName = string.IsNullOrEmpty(folderPath) ? fileName : $"{folderPath}/{fileName}";

                await _storageClient.UploadObjectAsync(_bucketName, objectName, file.ContentType, stream);

                // Return the public URL
                var publicUrl = $"https://storage.googleapis.com/{_bucketName}/{objectName}";

                _logger.LogInformation("File uploaded successfully: {FileName} to {Url}", fileName, publicUrl);

                return publicUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file {FileName} to Google Cloud Storage", file.FileName);
                throw;
            }
        }

        public async Task<string> UploadFileAsync(byte[] fileBytes, string fileName, string folderPath, string contentType)
        {
            if (fileBytes == null || fileBytes.Length == 0)
                throw new ArgumentException("File bytes are null or empty", nameof(fileBytes));

            var uniqueFileName = GenerateUniqueFileName(fileName, folderPath);

            try
            {
                using var stream = new MemoryStream(fileBytes);

                var objectName = string.IsNullOrEmpty(folderPath) ? uniqueFileName : $"{folderPath}/{uniqueFileName}";

                await _storageClient.UploadObjectAsync(_bucketName, objectName, contentType, stream);

                var publicUrl = $"https://storage.googleapis.com/{_bucketName}/{objectName}";

                _logger.LogInformation("File uploaded successfully: {FileName} to {Url}", uniqueFileName, publicUrl);

                return publicUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file bytes {FileName} to Google Cloud Storage", fileName);
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            try
            {
                // Extract object name from URL if it's a full URL
                var objectName = ExtractObjectNameFromUrl(filePath);

                await _storageClient.DeleteObjectAsync(_bucketName, objectName);

                _logger.LogInformation("File deleted successfully: {ObjectName}", objectName);
                return true;
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("File not found for deletion: {FilePath}", filePath);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file {FilePath} from Google Cloud Storage", filePath);
                return false;
            }
        }

        public async Task<Stream> DownloadFileAsync(string filePath)
        {
            try
            {
                var objectName = ExtractObjectNameFromUrl(filePath);
                var stream = new MemoryStream();

                await _storageClient.DownloadObjectAsync(_bucketName, objectName, stream);
                stream.Position = 0;

                return stream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file {FilePath} from Google Cloud Storage", filePath);
                throw;
            }
        }

        public async Task<bool> FileExistsAsync(string filePath)
        {
            try
            {
                var objectName = ExtractObjectNameFromUrl(filePath);
                var obj = await _storageClient.GetObjectAsync(_bucketName, objectName);
                return obj != null;
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file existence {FilePath} in Google Cloud Storage", filePath);
                return false;
            }
        }

        public async Task<IEnumerable<string>> ListFilesAsync(string folderPath = "")
        {
            try
            {
                var objects = _storageClient.ListObjectsAsync(_bucketName, folderPath);
                var fileNames = new List<string>();

                await foreach (var obj in objects)
                {
                    fileNames.Add(obj.Name);
                }

                return fileNames;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files in folder {FolderPath} from Google Cloud Storage", folderPath);
                throw;
            }
        }

        private string GenerateUniqueFileName(string originalFileName, string folderPath)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var guid = Guid.NewGuid().ToString("N")[..8];
            var extension = Path.GetExtension(originalFileName);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);

            // Sanitize filename
            var sanitizedName = SanitizeFileName(nameWithoutExtension);

            return $"{sanitizedName}_{timestamp}_{guid}{extension}";
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        }

        private string ExtractObjectNameFromUrl(string urlOrPath)
        {
            // If it's a full URL, extract the object name
            if (urlOrPath.StartsWith("https://storage.googleapis.com/"))
            {
                var uri = new Uri(urlOrPath);
                var pathSegments = uri.AbsolutePath.TrimStart('/').Split('/');
                // Skip bucket name (first segment) and return the rest
                return string.Join("/", pathSegments.Skip(1));
            }

            // If it's already an object name/path, return as is
            return urlOrPath.TrimStart('/');
        }
    }
}