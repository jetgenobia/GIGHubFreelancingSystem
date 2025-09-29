using System.Security.Cryptography;
using System.Text;

namespace Freelancing.Services
{
    public class IdentityEncryptionService : IIdentityEncryptionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<IdentityEncryptionService> _logger;

        public IdentityEncryptionService(IConfiguration configuration, ILogger<IdentityEncryptionService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public string EncryptIdentityData(string data, string userId)
        {
            if (string.IsNullOrEmpty(data))
                return data;

            try
            {
                using (var aes = Aes.Create())
                {
                    // Use AES-256 encryption
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    // Generate user-specific key
                    var keyBytes = DeriveUserKey(userId, 32); // 32 bytes = 256 bits
                    aes.Key = keyBytes;
                    aes.GenerateIV();

                    using (var encryptor = aes.CreateEncryptor())
                    using (var msEncrypt = new MemoryStream())
                    {
                        // Prepend IV to the encrypted data
                        msEncrypt.Write(aes.IV, 0, aes.IV.Length);

                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        using (var swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(data);
                        }

                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encrypt identity data for user {UserId}", userId);
                throw new InvalidOperationException("Failed to encrypt identity data");
            }
        }

        public string DecryptIdentityData(string encryptedData, string userId)
        {
            if (string.IsNullOrEmpty(encryptedData) || !IsEncrypted(encryptedData))
                return encryptedData;

            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedData);

                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    var keyBytes = DeriveUserKey(userId, 32);
                    aes.Key = keyBytes;

                    // Extract IV from the beginning of the encrypted data
                    var iv = new byte[aes.BlockSize / 8];
                    Array.Copy(encryptedBytes, 0, iv, 0, iv.Length);
                    aes.IV = iv;

                    // Get the actual encrypted content (without IV)
                    var encryptedContent = new byte[encryptedBytes.Length - iv.Length];
                    Array.Copy(encryptedBytes, iv.Length, encryptedContent, 0, encryptedContent.Length);

                    using (var decryptor = aes.CreateDecryptor())
                    using (var msDecrypt = new MemoryStream(encryptedContent))
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var srDecrypt = new StreamReader(csDecrypt))
                    {
                        return srDecrypt.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt identity data for user {UserId}", userId);
                throw new InvalidOperationException("Failed to decrypt identity data");
            }
        }

        public string EncryptDocumentImage(byte[] imageBytes, string userId)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return string.Empty;

            try
            {
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    var keyBytes = DeriveUserKey(userId, 32);
                    aes.Key = keyBytes;
                    aes.GenerateIV();

                    using (var encryptor = aes.CreateEncryptor())
                    using (var msEncrypt = new MemoryStream())
                    {
                        // Prepend IV to the encrypted data
                        msEncrypt.Write(aes.IV, 0, aes.IV.Length);

                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        {
                            csEncrypt.Write(imageBytes, 0, imageBytes.Length);
                        }

                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encrypt document image for user {UserId}", userId);
                throw new InvalidOperationException("Failed to encrypt document image");
            }
        }

        public byte[] DecryptDocumentImage(string encryptedImage, string userId)
        {
            if (string.IsNullOrEmpty(encryptedImage) || !IsEncrypted(encryptedImage))
                return Convert.FromBase64String(encryptedImage);

            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedImage);

                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    var keyBytes = DeriveUserKey(userId, 32);
                    aes.Key = keyBytes;

                    // Extract IV from the beginning of the encrypted data
                    var iv = new byte[aes.BlockSize / 8];
                    Array.Copy(encryptedBytes, 0, iv, 0, iv.Length);
                    aes.IV = iv;

                    // Get the actual encrypted content (without IV)
                    var encryptedContent = new byte[encryptedBytes.Length - iv.Length];
                    Array.Copy(encryptedBytes, iv.Length, encryptedContent, 0, encryptedContent.Length);

                    using (var decryptor = aes.CreateDecryptor())
                    using (var msDecrypt = new MemoryStream(encryptedContent))
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var msResult = new MemoryStream())
                    {
                        csDecrypt.CopyTo(msResult);
                        return msResult.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt document image for user {UserId}", userId);
                throw new InvalidOperationException("Failed to decrypt document image");
            }
        }

        public string GenerateUserKey(string userId)
        {
            try
            {
                var masterKey = _configuration["Encryption:MasterKey"] ?? 
                    throw new InvalidOperationException("Master key not configured");
                
                var input = $"{masterKey}:IdentityVerification:{userId}:{DateTime.UtcNow:yyyy-MM}";
                
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                    return Convert.ToBase64String(hashBytes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate user key for {UserId}", userId);
                throw;
            }
        }

        public bool IsEncrypted(string data)
        {
            if (string.IsNullOrEmpty(data))
                return false;

            try
            {
                // Check if the data is base64 encoded and has sufficient length for IV + encrypted content
                var bytes = Convert.FromBase64String(data);
                return bytes.Length > 16; // IV is 16 bytes, so encrypted data should be longer
            }
            catch
            {
                return false;
            }
        }

        private byte[] DeriveUserKey(string userId, int keySize)
        {
            var masterKey = _configuration["Encryption:MasterKey"] ?? 
                throw new InvalidOperationException("Master key not configured");
            
            // Create a deterministic salt for the user
            var saltString = $"IdentityVerification:{userId}:{masterKey}:2024";
            var salt = Encoding.UTF8.GetBytes(saltString).Take(16).ToArray();

            using (var pbkdf2 = new Rfc2898DeriveBytes(
                userId, 
                salt, 
                int.Parse(_configuration["Encryption:KeyDerivationIterations"] ?? "100000"), 
                HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(keySize);
            }
        }
    }
}
