using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Storage.V1;
using System.Text.Json;

namespace Freelancing.Services
{
    public class GoogleCloudStorageService : IGoogleCloudStorageService
    {
        private readonly StorageClient _storageClient;
        private readonly string _bucketName;
        private readonly ILogger<GoogleCloudStorageService> _logger;
        private readonly GoogleCredential _credential; // ADD THIS

        public GoogleCloudStorageService(IConfiguration configuration, ILogger<GoogleCloudStorageService> logger)
        {
            _logger = logger;
            _bucketName = configuration["GoogleCloud:BucketName"] ??
                         Environment.GetEnvironmentVariable("GOOGLE_CLOUD_BUCKET_NAME") ??
                         throw new InvalidOperationException("Google Cloud bucket name not configured");

            try
            {
                var credentialsJson = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_CREDENTIALS");

                if (!string.IsNullOrEmpty(credentialsJson))
                {
                    _credential = GoogleCredential.FromJson(credentialsJson); // STORE CREDENTIAL
                    _storageClient = StorageClient.Create(_credential);
                }
                else
                {
                    _credential = GoogleCredential.GetApplicationDefault(); // STORE CREDENTIAL
                    _storageClient = StorageClient.Create(_credential);
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

                // CHANGE: Maximum 7 days for signed URLs
                var signedUrl = await GenerateSignedUrlAsync(objectName, TimeSpan.FromDays(7));

                _logger.LogInformation("File uploaded successfully: {FileName} to signed URL", fileName);

                return signedUrl;
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

                // CHANGE: Maximum 7 days for signed URLs
                var signedUrl = await GenerateSignedUrlAsync(objectName, TimeSpan.FromDays(7));

                _logger.LogInformation("File uploaded successfully: {FileName} to signed URL", uniqueFileName);

                return signedUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file bytes {FileName} to Google Cloud Storage", fileName);
                throw;
            }
        }

        // ADD THIS NEW METHOD
        public async Task<string> GenerateSignedUrlAsync(string objectName, TimeSpan expiration)
        {
            try
            {
                var serviceAccountCredential = (ServiceAccountCredential)_credential.UnderlyingCredential;
                var urlSigner = UrlSigner.FromServiceAccountCredential(serviceAccountCredential);

                var signedUrl = await urlSigner.SignAsync(_bucketName, objectName, expiration, HttpMethod.Get);
                return signedUrl.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating signed URL for {ObjectName}", objectName);
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            try
            {
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
            // Handle signed URLs - extract object name from path
            if (urlOrPath.Contains("storage.googleapis.com") || urlOrPath.Contains("storage.cloud.google.com"))
            {
                try
                {
                    var uri = new Uri(urlOrPath);
                    var pathSegments = uri.AbsolutePath.TrimStart('/').Split('/');

                    // For signed URLs, the format is usually /bucket-name/object-name
                    if (pathSegments.Length >= 2)
                    {
                        return string.Join("/", pathSegments.Skip(1));
                    }
                }
                catch
                {
                    // If URL parsing fails, treat as object name
                }
            }

            return urlOrPath.TrimStart('/');
        }
    }
}