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

        public async Task<Contract> CreateContractFromBiddingAsync(Guid projectId, Guid biddingId, Guid? selectedTemplateId = null)
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

            // Get template - use selected template if provided, otherwise fall back to category-based selection
            ContractTemplate? template = null;
            if (selectedTemplateId.HasValue)
            {
                template = await _context.ContractTemplates
                    .Where(ct => ct.Id == selectedTemplateId.Value && ct.IsActive)
                    .FirstOrDefaultAsync();
            }
            
            if (template == null)
            {
                template = await GetContractTemplateAsync(project.Category);
            }
            
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

        public async Task UpdateContractContentWithTermsAsync(Guid contractId, string paymentTermsJson, string revisionPolicyJson, string timelineJson)
        {
            var contract = await _context.Contracts.FindAsync(contractId);
            if (contract == null)
                throw new ArgumentException("Contract not found");

            // Parse the JSON data
            var paymentTerms = JsonSerializer.Deserialize<JsonElement>(paymentTermsJson);
            var revisionPolicy = JsonSerializer.Deserialize<JsonElement>(revisionPolicyJson);
            var timeline = JsonSerializer.Deserialize<JsonElement>(timelineJson);

            // Generate the actual content sections
            var paymentTermsSection = GeneratePaymentTermsSection(paymentTerms);
            var revisionPolicySection = GenerateRevisionPolicySection(revisionPolicy);
            var timelineSection = GenerateTimelineSection(timeline);

            // Update the contract content with actual sections
            var updatedContent = contract.ContractContent
                .Replace("<p><strong>Payment Structure:</strong></p><ul><li>Payment terms will be specified in the contract details</li></ul>", paymentTermsSection)
                .Replace("<p><strong>Revision Terms:</strong></p><ul><li>Revision policy will be specified in the contract details</li></ul>", revisionPolicySection)
                .Replace("<p><strong>Project Schedule:</strong></p><ul><li>Project timeline will be specified in the contract details</li></ul>", timelineSection);

            contract.ContractContent = updatedContent;
            contract.LastModifiedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        private string GeneratePaymentTermsSection(JsonElement paymentTerms)
        {
            var upfront = paymentTerms.GetProperty("upfront").GetInt32();
            var final = paymentTerms.GetProperty("final").GetInt32();
            var milestones = paymentTerms.GetProperty("milestones");

            var section = $"<p><strong>Payment Structure:</strong></p><ul>";
            section += $"<li>Upfront Payment: {upfront}% - Due upon contract signing</li>";
            
            if (milestones.GetArrayLength() > 0)
            {
                foreach (var milestone in milestones.EnumerateArray())
                {
                    var name = milestone.GetProperty("name").GetString();
                    var percentage = milestone.GetProperty("percentage").GetInt32();
                    var dueDate = milestone.GetProperty("dueDate").GetString();
                    section += $"<li>{name}: {percentage}% - Due {dueDate}</li>";
                }
            }
            
            section += $"<li>Final Payment: {final}% - Due upon project completion</li>";
            section += "</ul><p>All payments will be made according to the agreed schedule. Late payments may incur additional charges.</p>";

            return section;
        }

        private string GenerateRevisionPolicySection(JsonElement revisionPolicy)
        {
            var freeRevisions = revisionPolicy.GetProperty("freeRevisions").GetInt32();
            var additionalCost = revisionPolicy.GetProperty("additionalCost").GetDecimal();
            var scope = revisionPolicy.GetProperty("scope").GetString();

            var section = $"<p><strong>Revision Terms:</strong></p><ul>";
            section += $"<li>Free Revisions: {freeRevisions} revision round(s) included in the project cost</li>";
            section += $"<li>Additional Revisions: ₱{additionalCost:N0} per additional revision round</li>";
            section += $"<li>Revision Scope: {scope}</li>";
            section += "</ul><p>Each revision round includes feedback incorporation and refinements as agreed upon by both parties.</p>";

            return section;
        }

        private string GenerateTimelineSection(JsonElement timeline)
        {
            var startDate = timeline.GetProperty("startDate").GetString();
            var deadline = timeline.GetProperty("deadline").GetString();
            var milestones = timeline.GetProperty("milestones");

            var section = $"<p><strong>Project Schedule:</strong></p><ul>";
            section += $"<li>Project Start Date: {startDate}</li>";
            section += $"<li>Project Deadline: {deadline}</li>";
            
            if (milestones.GetArrayLength() > 0)
            {
                foreach (var milestone in milestones.EnumerateArray())
                {
                    var name = milestone.GetProperty("name").GetString();
                    var dueDate = milestone.GetProperty("dueDate").GetString();
                    section += $"<li>{name}: Due {dueDate}</li>";
                }
            }
            
            section += "</ul><p>Timeline is subject to timely feedback and approvals from the Client. Delays in client feedback may extend the project timeline accordingly.</p>";

            return section;
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

        public async Task<Contract> UpdateContractContentAsync(Guid contractId, string newContent, string userId)
        {
            var contract = await GetContractByIdAsync(contractId);
            if (contract == null)
                throw new ArgumentException("Contract not found");

            // Create revision before updating
            await CreateContractRevisionAsync(contractId, contract.ContractContent, "Contract content updated", userId);

            contract.ContractContent = newContent;
            contract.LastModifiedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await LogContractActionAsync(contractId, userId, "Modified", "Contract content updated");

            return contract;
        }

        public async Task<ContractTemplate?> GetContractTemplateAsync(string category)
        {
            // Try to find category-specific template
            var template = await _context.ContractTemplates
                .Where(ct => ct.Category.ToLower() == category.ToLower() && ct.IsActive)
                .FirstOrDefaultAsync();

            // If not found, get any active template
            if (template == null)
            {
                template = await _context.ContractTemplates
                    .Where(ct => ct.IsActive)
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
                {"{{CONTRACT_DATE}}", DateTime.UtcNow.ToString("MMMM dd, yyyy")},
                {"{{PROPOSAL_DETAILS}}", bidding.Proposal}
            };

            foreach (var replacement in replacements)
            {
                content = content.Replace(replacement.Key, replacement.Value);
            }

            // Replace section placeholders with default content (will be updated later)
            content = content.Replace("{{PAYMENT_TERMS_SECTION}}", 
                "<p><strong>Payment Structure:</strong></p>" +
                "<ul>" +
                "<li>Payment terms will be specified in the contract details</li>" +
                "</ul>");

            content = content.Replace("{{REVISION_POLICY_SECTION}}", 
                "<p><strong>Revision Terms:</strong></p>" +
                "<ul>" +
                "<li>Revision policy will be specified in the contract details</li>" +
                "</ul>");

            content = content.Replace("{{PROJECT_TIMELINE_SECTION}}", 
                "<p><strong>Project Schedule:</strong></p>" +
                "<ul>" +
                "<li>Project timeline will be specified in the contract details</li>" +
                "</ul>");

            return content;
        }

        public async Task<Contract> SignContractAsync(Guid contractId, string userId, string signatureType, string signatureData, string ipAddress, string userAgent)
        {
            var contract = await GetContractByIdAsync(contractId);
            if (contract == null)
                throw new ArgumentException("Contract not found");

            var isClient = contract.Project.UserId == userId;
            var isFreelancer = contract.Bidding.UserId == userId;

            if (!isClient && !isFreelancer)
                throw new UnauthorizedAccessException("User not authorized to sign this contract");

            var now = DateTime.UtcNow;

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
                else if (contract.Status == "AwaitingClient")
                {
                    contract.Status = "Active";
                    
                    // Update project status to Active
                    contract.Project.Status = "Active";
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
                
                if (contract.Status == "Draft")
                {
                    contract.Status = "AwaitingClient";
                }
                else if (contract.Status == "AwaitingFreelancer")
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

        public async Task<bool> CanUserSignContractAsync(Guid contractId, string userId)
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

            var fileName = $"contract_{contractId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(uploadsDir, fileName);

            await File.WriteAllBytesAsync(filePath, pdfData);

            return $"/uploads/contracts/{fileName}";
        }

        public async Task LogContractActionAsync(Guid contractId, string userId, string action, string? details = null, string? ipAddress = null, string? userAgent = null)
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

        public async Task UpdateContractStatusAsync(Guid contractId, string newStatus, string userId)
        {
            var contract = await _context.Contracts.FindAsync(contractId);
            if (contract == null)
                throw new ArgumentException("Contract not found");

            var oldStatus = contract.Status;
            contract.Status = newStatus;
            contract.LastModifiedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await LogContractActionAsync(contractId, userId, "StatusChanged", $"Status changed from {oldStatus} to {newStatus}");
        }

        public async Task<List<Contract>> GetContractsByUserIdAsync(string userId, string? status = null)
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

        public async Task<ContractRevision> CreateContractRevisionAsync(Guid contractId, string newContent, string revisionNotes, string userId)
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
