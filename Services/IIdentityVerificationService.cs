using Freelancing.Models;
using Freelancing.Models.Entities;

namespace Freelancing.Services
{
    public interface IIdentityVerificationService
    {
        Task<VerificationResultViewModel> VerifyIdentityAsync(IdentityVerificationViewModel model, string userId);
        Task<VerificationResultViewModel> CompleteVerificationAsync(string liveFaceImageData, string userId, string idDocumentType, string idDocumentNumber, DateTime? idDocumentExpiryDate, bool idDocumentHasNoExpiration, bool idDocumentVerified, float idDocumentConfidence);
        Task<(bool verified, string message, float confidence)> VerifyIdDocumentAsync(IFormFile documentImage, string idDocumentType, string idDocumentNumber, DateTime? idDocumentExpiryDate, bool idDocumentHasNoExpiration, string userId);
        Task<(bool verified, string message, float confidence)> VerifyLiveFaceAsync(string base64ImageData, string userId);
        Task<VerificationStatusViewModel?> GetVerificationStatusAsync(string userId);
        Task<bool> IsUserVerifiedAsync(string userId);
        Task<bool> CanUserPostProjectAsync(string userId);
        Task<bool> CanUserBidAsync(string userId);
        Task<IdentityVerification?> GetLatestVerificationAsync(string userId);
        Task<bool> UpdateVerificationStatusAsync(Guid verificationId, string status, string? reason = null);
    }
}
