using Freelancing.Data;
using Freelancing.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Freelancing.Services
{
    public class ContractTerminationService : IContractTerminationService
    {
        private readonly ApplicationDbContext _context;

        public ContractTerminationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ContractTermination> CreateTerminationRequestAsync(Guid contractId, string userId, string reason, string details, decimal finalPayment, string? settlementNotes)
        {
            var contract = await _context.Contracts
                .Include(c => c.Project)
                .Include(c => c.Bidding)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new ArgumentException("Contract not found");

            if (contract.Status != "Active")
                throw new InvalidOperationException("Only active contracts can be terminated");

            var isClient = contract.Project.UserId == userId;
            var isFreelancer = contract.Bidding.UserId == userId;

            if (!isClient && !isFreelancer)
                throw new UnauthorizedAccessException("User is not authorized to terminate this contract");

            var userRole = isClient ? "Client" : "Freelancer";

            var termination = new ContractTermination
            {
                ContractId = contractId,
                TerminationReason = reason,
                TerminationDetails = details,
                FinalPayment = finalPayment,
                SettlementNotes = settlementNotes,
                RequestedAt = DateTime.UtcNow,
                RequestedByUserId = userId,
                RequestedByUserRole = userRole,
                Status = "Pending"
            };

            _context.ContractTerminations.Add(termination);
            var result = await _context.SaveChangesAsync();
            
            // Debug: Check if save was successful
            if (result == 0)
            {
                throw new InvalidOperationException("Failed to save termination request to database");
            }

            // Debug: Log the termination ID
            Console.WriteLine($"Termination created with ID: {termination.Id}");

            await LogTerminationActionAsync(termination.Id, userId, "Requested", $"Termination requested by {userRole}");

            return termination;
        }

        public async Task<ContractTermination?> GetTerminationByIdAsync(Guid terminationId)
        {
            return await _context.ContractTerminations
                .Include(ct => ct.Contract)
                    .ThenInclude(c => c.Project)
                        .ThenInclude(p => p.User)
                .Include(ct => ct.Contract)
                    .ThenInclude(c => c.Bidding)
                        .ThenInclude(b => b.User)
                .FirstOrDefaultAsync(ct => ct.Id == terminationId);
        }

        public async Task<ContractTermination?> GetTerminationByContractIdAsync(Guid contractId)
        {
            return await _context.ContractTerminations
                .Include(ct => ct.Contract)
                    .ThenInclude(c => c.Project)
                        .ThenInclude(p => p.User)
                .Include(ct => ct.Contract)
                    .ThenInclude(c => c.Bidding)
                        .ThenInclude(b => b.User)
                .FirstOrDefaultAsync(ct => ct.ContractId == contractId && ct.Status != "Cancelled");
        }

        public async Task<List<ContractTermination>> GetTerminationsByUserIdAsync(string userId, string? status = null)
        {
            var query = _context.ContractTerminations
                .Include(ct => ct.Contract)
                    .ThenInclude(c => c.Project)
                        .ThenInclude(p => p.User)
                .Include(ct => ct.Contract)
                    .ThenInclude(c => c.Bidding)
                        .ThenInclude(b => b.User)
                .Where(ct => ct.RequestedByUserId == userId || 
                           ct.Contract.Project.UserId == userId || 
                           ct.Contract.Bidding.UserId == userId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(ct => ct.Status == status);
            }

            return await query.OrderByDescending(ct => ct.RequestedAt).ToListAsync();
        }

        public async Task<ContractTermination> SignTerminationAsync(Guid terminationId, string userId, string signatureType, string signatureData, string ipAddress, string userAgent)
        {
            var termination = await GetTerminationByIdAsync(terminationId);
            if (termination == null)
                throw new ArgumentException("Termination request not found");

            var isClient = termination.Contract.Project.UserId == userId;
            var isFreelancer = termination.Contract.Bidding.UserId == userId;

            if (!isClient && !isFreelancer)
                throw new UnauthorizedAccessException("User is not authorized to sign this termination");

            if (isClient)
            {
                if (termination.ClientSignedAt.HasValue)
                    throw new InvalidOperationException("Client has already signed this termination");

                termination.ClientSignedAt = DateTime.UtcNow;
                termination.ClientSignatureType = signatureType;
                termination.ClientSignatureData = signatureData;
                termination.ClientIPAddress = ipAddress;
                termination.ClientUserAgent = userAgent;

                termination.Status = termination.FreelancerSignedAt.HasValue ? "Signed" : "AwaitingFreelancer";
            }
            else
            {
                if (termination.FreelancerSignedAt.HasValue)
                    throw new InvalidOperationException("Freelancer has already signed this termination");

                termination.FreelancerSignedAt = DateTime.UtcNow;
                termination.FreelancerSignatureType = signatureType;
                termination.FreelancerSignatureData = signatureData;
                termination.FreelancerIPAddress = ipAddress;
                termination.FreelancerUserAgent = userAgent;

                termination.Status = termination.ClientSignedAt.HasValue ? "Signed" : "AwaitingClient";
            }

            if (termination.Status == "Signed")
            {
                termination.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await LogTerminationActionAsync(terminationId, userId, "Signed", $"Termination signed by {(isClient ? "Client" : "Freelancer")}");

            return termination;
        }

        public async Task<bool> IsTerminationFullySignedAsync(Guid terminationId)
        {
            var termination = await _context.ContractTerminations
                .FirstOrDefaultAsync(ct => ct.Id == terminationId);

            return termination?.ClientSignedAt.HasValue == true && termination?.FreelancerSignedAt.HasValue == true;
        }

        public async Task<bool> CanUserSignTerminationAsync(Guid terminationId, string userId)
        {
            var termination = await GetTerminationByIdAsync(terminationId);
            if (termination == null)
                return false;

            if (termination.Status != "Pending" && termination.Status != "AwaitingClient" && termination.Status != "AwaitingFreelancer")
                return false;

            var isClient = termination.Contract.Project.UserId == userId;
            var isFreelancer = termination.Contract.Bidding.UserId == userId;

            if (!isClient && !isFreelancer)
                return false;

            if (isClient && termination.ClientSignedAt.HasValue)
                return false;

            if (isFreelancer && termination.FreelancerSignedAt.HasValue)
                return false;

            return true;
        }

        public async Task UpdateTerminationStatusAsync(Guid terminationId, string newStatus, string userId)
        {
            var termination = await _context.ContractTerminations.FindAsync(terminationId);
            if (termination == null)
                throw new ArgumentException("Termination request not found");

            termination.Status = newStatus;
            await _context.SaveChangesAsync();

            await LogTerminationActionAsync(terminationId, userId, "StatusUpdated", $"Status updated to {newStatus}");
        }

        public async Task CancelTerminationAsync(Guid terminationId, string userId)
        {
            var termination = await GetTerminationByIdAsync(terminationId);
            if (termination == null)
                throw new ArgumentException("Termination request not found");

            if (termination.RequestedByUserId != userId)
                throw new UnauthorizedAccessException("Only the requester can cancel the termination");

            termination.Status = "Cancelled";
            await _context.SaveChangesAsync();

            await LogTerminationActionAsync(terminationId, userId, "Cancelled", "Termination request cancelled");
        }

        public async Task<byte[]> GenerateTerminationPdfAsync(Guid terminationId)
        {
            // Placeholder implementation
            return new byte[0];
        }

        public async Task<string> SaveSignedTerminationPdfAsync(Guid terminationId, byte[] pdfData)
        {
            // Placeholder implementation
            return "";
        }

        public async Task LogTerminationActionAsync(Guid terminationId, string userId, string action, string? details = null, string? ipAddress = null, string? userAgent = null)
        {
            var auditLog = new ContractTerminationAuditLog
            {
                ContractTerminationId = terminationId,
                UserId = userId,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow,
                IPAddress = ipAddress,
                UserAgent = userAgent
            };

            _context.ContractTerminationAuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }

        public async Task<string> CalculateTerminationDocumentHashAsync(string content)
        {
            // Placeholder implementation
            return "";
        }

        public async Task<bool> VerifyTerminationDocumentIntegrityAsync(Guid terminationId)
        {
            // Placeholder implementation
            return true;
        }

        public async Task ExecuteTerminationAsync(Guid terminationId, string userId)
        {
            var termination = await GetTerminationByIdAsync(terminationId);
            if (termination == null)
                throw new ArgumentException("Termination request not found");

            if (termination.Status != "Signed")
                throw new InvalidOperationException("Termination must be fully signed before contract can be terminated");

            termination.Status = "Completed";
            termination.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await LogTerminationActionAsync(terminationId, userId, "Executed", "Termination executed - contract terminated");
        }

        public async Task TerminateContractAsync(Guid terminationId, string userId)
        {
            var termination = await GetTerminationByIdAsync(terminationId);
            if (termination == null)
                throw new ArgumentException("Termination request not found");

            if (termination.Status != "Signed")
                throw new InvalidOperationException("Termination must be fully signed before contract can be terminated");

            var contract = termination.Contract;
            if (contract == null)
                throw new InvalidOperationException("Associated contract not found for termination");

            contract.Status = "Terminated";
            contract.LastModifiedAt = DateTime.UtcNow;

            if (contract.Project != null)
            {
                contract.Project.Status = "Terminated";
            }

            termination.Status = "Completed";
            termination.CompletedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await LogTerminationActionAsync(terminationId, userId, "ContractTerminated", "Contract successfully terminated");
        }

        public async Task<List<ContractTerminationAuditLog>> GetTerminationAuditLogsAsync(Guid terminationId)
        {
            return await _context.ContractTerminationAuditLogs
                .Include(cal => cal.User)
                .Where(cal => cal.ContractTerminationId == terminationId)
                .OrderByDescending(cal => cal.Timestamp)
                .ToListAsync();
        }
    }
}
