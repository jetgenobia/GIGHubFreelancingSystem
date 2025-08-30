using Freelancing.Models.Entities;

namespace Freelancing.Services
{
    public interface IContractTerminationService
    {
        // Termination Request Management
        Task<ContractTermination> CreateTerminationRequestAsync(Guid contractId, string userId, string reason, string details, decimal finalPayment, string? settlementNotes);
        Task<ContractTermination?> GetTerminationByIdAsync(Guid terminationId);
        Task<ContractTermination?> GetTerminationByContractIdAsync(Guid contractId);
        Task<List<ContractTermination>> GetTerminationsByUserIdAsync(string userId, string? status = null);
        
        // Signature Management
        Task<ContractTermination> SignTerminationAsync(Guid terminationId, string userId, string signatureType, string signatureData, string ipAddress, string userAgent);
        Task<bool> IsTerminationFullySignedAsync(Guid terminationId);
        Task<bool> CanUserSignTerminationAsync(Guid terminationId, string userId);
        
        // Status Management
        Task UpdateTerminationStatusAsync(Guid terminationId, string newStatus, string userId);
        Task CancelTerminationAsync(Guid terminationId, string userId);
        Task ExecuteTerminationAsync(Guid terminationId, string userId);
        
        // PDF Generation
        Task<byte[]> GenerateTerminationPdfAsync(Guid terminationId);
        Task<string> SaveSignedTerminationPdfAsync(Guid terminationId, byte[] pdfData);
        
        // Audit & Security
        Task LogTerminationActionAsync(Guid terminationId, string userId, string action, string? details = null, string? ipAddress = null, string? userAgent = null);
        Task<string> CalculateTerminationDocumentHashAsync(string content);
        Task<bool> VerifyTerminationDocumentIntegrityAsync(Guid terminationId);
        
        // Contract Termination
        Task TerminateContractAsync(Guid terminationId, string userId);
        Task<List<ContractTerminationAuditLog>> GetTerminationAuditLogsAsync(Guid terminationId);
    }
}
