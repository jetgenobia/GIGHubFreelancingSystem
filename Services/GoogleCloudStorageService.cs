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
        private readonly GoogleCredential _credential;

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
                    _credential = GoogleCredential.FromJson(credentialsJson)
                        .CreateScoped(new[] { "https://www.googleapis.com/auth/cloud-platform" });
                    _storageClient = StorageClient.Create(_credential);
                }
                else
                {
                    _credential = GoogleCredential.GetApplicationDefault()
                        .CreateScoped(new[] { "https://www.googleapis.com/auth/cloud-platform" });
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

                // Upload the object first
                await _storageClient.UploadObjectAsync(_bucketName, objectName, file.ContentType, stream);

                // Then make it public
                await MakeObjectPublicAsync(objectName);

                // Return direct public URL
                var publicUrl = $"https://storage.googleapis.com/{_bucketName}/{objectName}";

                _logger.LogInformation("File uploaded successfully as public: {FileName} at {PublicUrl}", fileName, publicUrl);

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

                // Upload the object first
                await _storageClient.UploadObjectAsync(_bucketName, objectName, contentType, stream);

                // Then make it public
                await MakeObjectPublicAsync(objectName);

                // Return direct public URL
                var publicUrl = $"https://storage.googleapis.com/{_bucketName}/{objectName}";

                _logger.LogInformation("File uploaded successfully as public: {FileName} at {PublicUrl}", uniqueFileName, publicUrl);

                return publicUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file bytes {FileName} to Google Cloud Storage", fileName);
                throw;
            }
        }

        private async Task MakeObjectPublicAsync(string objectName)
        {
            try
            {
                // Use the REST API to set object ACL (this is the correct approach)
                using var httpClient = new HttpClient();

                // Get access token
                var accessToken = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync("https://oauth2.googleapis.com/token");

                // Set authorization header
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                // Create the ACL payload
                var aclPayload = new
                {
                    entity = "allUsers",
                    role = "READER"
                };

                var json = JsonSerializer.Serialize(aclPayload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Make the API call to add public access
                var encodedObjectName = Uri.EscapeDataString(objectName);
                var url = $"https://storage.googleapis.com/storage/v1/b/{_bucketName}/o/{encodedObjectName}/acl";

                var response = await httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully made object {ObjectName} public", objectName);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to make object {ObjectName} public. Status: {StatusCode}, Error: {Error}",
                        objectName, response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not set public ACL for {ObjectName}, object uploaded but may not be publicly accessible", objectName);
            }
        }

        public async Task<string> GenerateSignedUrlAsync(string objectName, TimeSpan expiration)
        {
            try
            {
                // FIX: Proper credential handling for signed URLs
                var credentialsJson = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_CREDENTIALS");
                if (string.IsNullOrEmpty(credentialsJson))
                {
                    throw new InvalidOperationException("Google Cloud credentials not found");
                }

                // Parse the JSON to extract the service account information
                var credentialData = JsonSerializer.Deserialize<Dictionary<string, object>>(credentialsJson);
                var clientEmail = credentialData["client_email"].ToString();
                var privateKey = credentialData["private_key"].ToString();

                // Create service account credential directly from the JSON
                var serviceAccountCredential = new ServiceAccountCredential(
                    new ServiceAccountCredential.Initializer(clientEmail)
                    {
                        Scopes = new[] { "https://www.googleapis.com/auth/cloud-platform" }
                    }.FromPrivateKey(privateKey));

                var urlSigner = UrlSigner.FromServiceAccountCredential(serviceAccountCredential);

                var signedUrl = await urlSigner.SignAsync(
                    _bucketName,
                    objectName,
                    expiration,
                    HttpMethod.Get);

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

        public async Task SetBucketCorsAsync()
        {
            try
            {
                var bucket = await _storageClient.GetBucketAsync(_bucketName);

                bucket.Cors = new List<Bucket.CorsData>
        {
            new Bucket.CorsData
            {
                Origin = new List<string> { "*" },
                Method = new List<string> { "GET", "HEAD" },
                ResponseHeader = new List<string> { "Content-Type", "Access-Control-Allow-Origin" },
                MaxAgeSeconds = 3600
            }
        };

                await _storageClient.UpdateBucketAsync(bucket);

                _logger.LogInformation("CORS configuration set successfully for bucket {BucketName}", _bucketName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting CORS configuration for bucket {BucketName}", _bucketName);
                throw;
            }
        }
    }
}