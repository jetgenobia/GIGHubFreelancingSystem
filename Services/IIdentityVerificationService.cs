using Freelancing.Models;
using Freelancing.Models.Entities;
using Microsoft.AspNetCore.Http;

namespace Freelancing.Services
{
    public interface IIdentityVerificationService
    {
        Task<VerificationResultViewModel> VerifyIdentityAsync(IdentityVerificationViewModel model, string userId);

        Task<VerificationResultViewModel> CompleteVerificationAsync(
            string liveFaceImageData,
            string userId,
            string idDocumentType,
            string idDocumentNumber,
            DateTime? idDocumentExpiryDate,
            bool idDocumentHasNoExpiration,
            bool idDocumentVerified,
            float idDocumentConfidence,
            string? extractedIdName);

        Task<(bool verified, string message, float confidence, string? extractedIdName, string? extractedIdNumber)> VerifyIdDocumentAsync(
            IFormFile? documentImage,
            string idDocumentType,
            string idDocumentNumber,
            DateTime? idDocumentExpiryDate,
            bool idDocumentHasNoExpiration,
            string userId,
            byte[]? storedImageBytes = null); // Add optional parameter for stored image data

        Task<(bool verified, string message, float confidence)> VerifyLiveFaceAsync(string base64ImageData, string userId);

        Task<VerificationStatusViewModel?> GetVerificationStatusAsync(string userId);

        Task<bool> IsUserVerifiedAsync(string userId);

        Task<bool> CanUserPostProjectAsync(string userId);

        Task<bool> CanUserBidAsync(string userId);

        Task<IdentityVerification?> GetLatestVerificationAsync(string userId);

        Task<bool> UpdateVerificationStatusAsync(Guid verificationId, string status, string? reason = null);
    }
}