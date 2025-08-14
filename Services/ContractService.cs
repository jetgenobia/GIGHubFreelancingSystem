using Freelancing.Data;
using Freelancing.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Freelancing.Services
{
    public class ContractService : IContractService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPdfGenerationService _pdfService;

        public ContractService(ApplicationDbContext context, IPdfGenerationService pdfService)
        {
            _context = context;
            _pdfService = pdfService;
        }

        public async Task<Contract> CreateContractFromBiddingAsync(Guid projectId, Guid biddingId)
        {
            var project = await _context.Projects
                .Include(p => p.User)
                .Include(p => p.ProjectSkills)
                    .ThenInclude(ps => ps.UserSkill)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            var bidding = await _context.Biddings
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.Id == biddingId);

            if (project == null || bidding == null)
                throw new ArgumentException("Project or bidding not found");

            // Get appropriate template
            var template = await GetContractTemplateAsync(project.Category);
            if (template == null)
                throw new InvalidOperationException("No contract template found for this category");

            // Generate contract content
            var contractContent = await GenerateContractContentAsync(project, bidding, template);

            var contract = new Contract
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                BiddingId = biddingId,
                ContractTitle = $"Freelance Contract - {project.ProjectName}",
                ContractContent = contractContent,
                ContractTemplateUsed = template.Name,
                Status = "Draft"
            };

            _context.Contracts.Add(contract);
            await _context.SaveChangesAsync();

            // Log contract creation
            await LogContractActionAsync(contract.Id, project.UserId, "Created", "Contract created from accepted bidding");

            return contract;
        }

        public async Task<Contract?> GetContractByProjectIdAsync(Guid projectId)
        {
            return await _context.Contracts
                .Include(c => c.Project)
                    .ThenInclude(p => p.User)
                .Include(c => c.Bidding)
                    .ThenInclude(b => b.User)
                .Include(c => c.AuditLogs)
                .FirstOrDefaultAsync(c => c.ProjectId == projectId);
        }

        public async Task<Contract?> GetContractByIdAsync(Guid contractId)
        {
            return await _context.Contracts
                .Include(c => c.Project)
                    .ThenInclude(p => p.User)
                .Include(c => c.Bidding)
                    .ThenInclude(b => b.User)
                .Include(c => c.AuditLogs)
                    .ThenInclude(al => al.User)
                .Include(c => c.Revisions)
                    .ThenInclude(r => r.CreatedByUser)
                .FirstOrDefaultAsync(c => c.Id == contractId);
        }

        public async Task<Contract> UpdateContractContentAsync(Guid contractId, string newContent, Guid userId)
        {
            var contract = await GetContractByIdAsync(contractId);
            if (contract == null)
                throw new ArgumentException("Contract not found");

            // Create revision before updating
            await CreateContractRevisionAsync(contractId, contract.ContractContent, "Contract content updated", userId);

            contract.ContractContent = newContent;
            contract.LastModifiedAt = DateTime.UtcNow.ToLocalTime();

            await _context.SaveChangesAsync();
            await LogContractActionAsync(contractId, userId, "Modified", "Contract content updated");

            return contract;
        }

        public async Task<ContractTemplate?> GetContractTemplateAsync(string category)
        {
            // First try to find category-specific template
            var template = await _context.ContractTemplates
                .Where(ct => ct.Category.ToLower() == category.ToLower() && ct.IsActive)
                .OrderBy(ct => ct.IsDefault ? 0 : 1)
                .FirstOrDefaultAsync();

            // If not found, get default template
            if (template == null)
            {
                template = await _context.ContractTemplates
                    .Where(ct => ct.IsDefault && ct.IsActive)
                    .FirstOrDefaultAsync();
            }

            return template;
        }

        public async Task<string> GenerateContractContentAsync(Project project, Bidding bidding, ContractTemplate template)
        {
            var content = template.TemplateContent;

            // Replace placeholders with actual data
            var replacements = new Dictionary<string, string>
            {
                {"{{PROJECT_NAME}}", project.ProjectName},
                {"{{PROJECT_DESCRIPTION}}", project.ProjectDescription},
                {"{{CLIENT_NAME}}", $"{project.User.FirstName} {project.User.LastName}"},
                {"{{CLIENT_EMAIL}}", project.User.Email},
                {"{{FREELANCER_NAME}}", $"{bidding.User.FirstName} {bidding.User.LastName}"},
                {"{{FREELANCER_EMAIL}}", bidding.User.Email},
                {"{{PROJECT_BUDGET}}", project.Budget},
                {"{{AGREED_AMOUNT}}", bidding.Budget.ToString()},
                {"{{DELIVERY_TIMELINE}}", bidding.Delivery},
                {"{{PROJECT_CATEGORY}}", project.Category},
                {"{{CONTRACT_DATE}}", DateTime.Now.ToString("MMMM dd, yyyy")},
                {"{{PROPOSAL_DETAILS}}", bidding.Proposal}
            };

            foreach (var replacement in replacements)
            {
                content = content.Replace(replacement.Key, replacement.Value);
            }

            return content;
        }

        public async Task<Contract> SignContractAsync(Guid contractId, Guid userId, string signatureType, string signatureData, string ipAddress, string userAgent)
        {
            var contract = await GetContractByIdAsync(contractId);
            if (contract == null)
                throw new ArgumentException("Contract not found");

            var isClient = contract.Project.UserId == userId;
            var isFreelancer = contract.Bidding.UserId == userId;

            if (!isClient && !isFreelancer)
                throw new UnauthorizedAccessException("User not authorized to sign this contract");

            var now = DateTime.UtcNow.ToLocalTime();

            if (isClient)
            {
                contract.ClientSignedAt = now;
                contract.ClientSignatureType = signatureType;
                contract.ClientSignatureData = signatureData;
                contract.ClientIPAddress = ipAddress;
                contract.ClientUserAgent = userAgent;
                
                await LogContractActionAsync(contractId, userId, "Signed", "Client signed the contract", ipAddress, userAgent);
                
                if (contract.Status == "Draft")
                {
                    contract.Status = "AwaitingFreelancer";
                }
            }
            else if (isFreelancer)
            {
                contract.FreelancerSignedAt = now;
                contract.FreelancerSignatureType = signatureType;
                contract.FreelancerSignatureData = signatureData;
                contract.FreelancerIPAddress = ipAddress;
                contract.FreelancerUserAgent = userAgent;
                
                await LogContractActionAsync(contractId, userId, "Signed", "Freelancer signed the contract", ipAddress, userAgent);
                
                if (contract.Status == "AwaitingFreelancer")
                {
                    contract.Status = "Active";
                    
                    // Update project status to Active
                    contract.Project.Status = "Active";
                }
            }

            // Check if contract is fully signed
            if (await IsContractFullySignedAsync(contractId))
            {
                // Generate and save signed PDF
                var pdfData = await GenerateContractPdfAsync(contractId);
                contract.DocumentPath = await SaveSignedContractPdfAsync(contractId, pdfData);
                contract.DocumentHash = await CalculateDocumentHashAsync(contract.ContractContent);
            }

            await _context.SaveChangesAsync();
            return contract;
        }

        public async Task<bool> IsContractFullySignedAsync(Guid contractId)
        {
            var contract = await _context.Contracts.FindAsync(contractId);
            return contract?.ClientSignedAt.HasValue == true && contract?.FreelancerSignedAt.HasValue == true;
        }

        public async Task<bool> CanUserSignContractAsync(Guid contractId, Guid userId)
        {
            var contract = await GetContractByIdAsync(contractId);
            if (contract == null) return false;

            var isClient = contract.Project.UserId == userId;
            var isFreelancer = contract.Bidding.UserId == userId;

            if (!isClient && !isFreelancer) return false;

            // Client can sign if they haven't signed yet
            if (isClient && !contract.ClientSignedAt.HasValue) return true;

            // Freelancer can sign if they haven't signed yet and client has signed
            if (isFreelancer && !contract.FreelancerSignedAt.HasValue) return true;

            return false;
        }

        public async Task<byte[]> GenerateContractPdfAsync(Guid contractId)
        {
            var contract = await GetContractByIdAsync(contractId);
            if (contract == null)
                throw new ArgumentException("Contract not found");

            return await _pdfService.GenerateContractPdfAsync(contract);
        }

        public async Task<string> SaveSignedContractPdfAsync(Guid contractId, byte[] pdfData)
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracts");
            if (!Directory.Exists(uploadsDir))
                Directory.CreateDirectory(uploadsDir);

            var fileName = $"contract_{contractId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(uploadsDir, fileName);

            await File.WriteAllBytesAsync(filePath, pdfData);

            return $"/uploads/contracts/{fileName}";
        }

        public async Task LogContractActionAsync(Guid contractId, Guid userId, string action, string? details = null, string? ipAddress = null, string? userAgent = null)
        {
            var auditLog = new ContractAuditLog
            {
                Id = Guid.NewGuid(),
                ContractId = contractId,
                UserId = userId,
                Action = action,
                Details = details,
                IPAddress = ipAddress,
                UserAgent = userAgent
            };

            _context.ContractAuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }

        public async Task<string> CalculateDocumentHashAsync(string content)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
            return Convert.ToBase64String(hash);
        }

        public async Task<bool> VerifyDocumentIntegrityAsync(Guid contractId)
        {
            var contract = await _context.Contracts.FindAsync(contractId);
            if (contract == null || string.IsNullOrEmpty(contract.DocumentHash))
                return false;

            var currentHash = await CalculateDocumentHashAsync(contract.ContractContent);
            return contract.DocumentHash == currentHash;
        }

        public async Task UpdateContractStatusAsync(Guid contractId, string newStatus, Guid userId)
        {
            var contract = await _context.Contracts.FindAsync(contractId);
            if (contract == null)
                throw new ArgumentException("Contract not found");

            var oldStatus = contract.Status;
            contract.Status = newStatus;
            contract.LastModifiedAt = DateTime.UtcNow.ToLocalTime();

            await _context.SaveChangesAsync();

            await LogContractActionAsync(contractId, userId, "StatusChanged", $"Status changed from {oldStatus} to {newStatus}");
        }

        public async Task<List<Contract>> GetContractsByUserIdAsync(Guid userId, string? status = null)
        {
            var query = _context.Contracts
                .Include(c => c.Project)
                    .ThenInclude(p => p.User)
                .Include(c => c.Bidding)
                    .ThenInclude(b => b.User)
                .Include(c => c.AuditLogs)
                    .ThenInclude(al => al.User)
                .Include(c => c.Revisions)
                    .ThenInclude(r => r.CreatedByUser)
                .Where(c => c.Project.UserId == userId || c.Bidding.UserId == userId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(c => c.Status == status);
            }

            return await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        }

        public async Task<List<ContractAuditLog>> GetContractAuditLogsAsync(Guid contractId)
        {
            return await _context.ContractAuditLogs
                .Include(cal => cal.User)
                .Where(cal => cal.ContractId == contractId)
                .OrderByDescending(cal => cal.Timestamp)
                .ToListAsync();
        }

        public async Task<ContractRevision> CreateContractRevisionAsync(Guid contractId, string newContent, string revisionNotes, Guid userId)
        {
            var contract = await _context.Contracts
                .Include(c => c.Revisions)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new ArgumentException("Contract not found");

            var revisionNumber = (contract.Revisions?.Count ?? 0) + 1;
            var previousHash = contract.Revisions?.LastOrDefault()?.CurrentHash ?? "";

            var revision = new ContractRevision
            {
                Id = Guid.NewGuid(),
                ContractId = contractId,
                RevisionNumber = revisionNumber,
                RevisionContent = newContent,
                RevisionNotes = revisionNotes,
                CreatedByUserId = userId,
                PreviousHash = previousHash,
                CurrentHash = await CalculateDocumentHashAsync(newContent)
            };

            _context.ContractRevisions.Add(revision);
            await _context.SaveChangesAsync();

            return revision;
        }

        public async Task<List<ContractRevision>> GetContractRevisionsAsync(Guid contractId)
        {
            return await _context.ContractRevisions
                .Include(cr => cr.CreatedByUser)
                .Where(cr => cr.ContractId == contractId)
                .OrderBy(cr => cr.RevisionNumber)
                .ToListAsync();
        }
    }
}
