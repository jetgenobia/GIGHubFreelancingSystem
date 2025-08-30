using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using Freelancing.Services;
using System.Security.Claims;
using System.Text.Json;

namespace Freelancing.Controllers
{
    [Authorize]
    public class ContractController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IContractService _contractService;
        private readonly INotificationService _notificationService;

        public ContractController(ApplicationDbContext context, IContractService contractService, INotificationService notificationService)
        {
            _context = context;
            _contractService = contractService;
            _notificationService = notificationService;
        }



        // GET: Contract/Create/{projectId}
        [HttpGet]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Create(Guid projectId)
        {
            var project = await _context.Projects
                .Include(p => p.User)
                .Include(p => p.AcceptedBid)
                    .ThenInclude(ab => ab.User)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null || project.AcceptedBidId == null)
                return NotFound("Project or accepted bid not found");

            var userId = GetCurrentUserId();
            if (project.UserId != userId)
                return Forbid("You are not authorized to create a contract for this project");
                
            // Debug logging
            System.Diagnostics.Debug.WriteLine($"Project Name: {project.ProjectName}");
            System.Diagnostics.Debug.WriteLine($"Project Budget: {project.Budget}");
            System.Diagnostics.Debug.WriteLine($"AcceptedBid Budget: {project.AcceptedBid?.Budget}");

            // Check if contract already exists
            var existingContract = await _contractService.GetContractByProjectIdAsync(projectId);
            if (existingContract != null)
                return RedirectToAction("Details", new { id = existingContract.Id });

            var availableTemplates = await GetAvailableTemplatesAsync(project.Category);

            var viewModel = new CreateContractViewModel
            {
                ProjectId = projectId,
                BiddingId = project.AcceptedBidId.Value,
                ProjectName = project.ProjectName ?? "",
                ProjectDescription = project.ProjectDescription ?? "",
                ProjectBudget = project.Budget ?? "0", // Budget is already a string
                ProjectCategory = project.Category ?? "",
                ClientName = $"{project.User?.FirstName ?? ""} {project.User?.LastName ?? ""}".Trim(),
                ClientEmail = project.User?.Email ?? "",
                FreelancerName = $"{project.AcceptedBid?.User?.FirstName ?? ""} {project.AcceptedBid?.User?.LastName ?? ""}".Trim(),
                FreelancerEmail = project.AcceptedBid?.User?.Email ?? "",
                AgreedAmount = project.AcceptedBid?.Budget ?? 0, // Budget is int
                DeliveryTimeline = project.AcceptedBid?.Delivery ?? "",
                Proposal = project.AcceptedBid?.Proposal ?? "",
                AvailableTemplates = availableTemplates
            };

            return View(viewModel);
        }

        // POST: Contract/Create
        [HttpPost]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Create(CreateContractViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableTemplates = await GetAvailableTemplatesAsync(model.ProjectCategory);
                return View(model);
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"Creating contract for Project: {model.ProjectId}, Bidding: {model.BiddingId}");
                
                var contract = await _contractService.CreateContractFromBiddingAsync(model.ProjectId, model.BiddingId, model.SelectedTemplateId);
                System.Diagnostics.Debug.WriteLine($"Contract created with ID: {contract.Id}");
                
                // Update contract with custom terms
                await UpdateContractTermsAsync(contract.Id, model);
                System.Diagnostics.Debug.WriteLine("Contract terms updated");

                // Send notification to freelancer
                var freelancerId = await GetFreelancerIdFromProjectAsync(model.ProjectId);
                if (!string.IsNullOrEmpty(freelancerId))
                {
                    // Get project name from database to ensure it's not null
                    var project = await _context.Projects.FindAsync(model.ProjectId);
                    var projectName = project?.ProjectName ?? "Unknown Project";
                    
                    await _notificationService.CreateNotificationAsync(
                        freelancerId,
                        "Contract Created",
                        $"A contract has been created for project '{projectName}'. Please review and sign.",
                        "contract_created",
                        "<svg viewBox=\"0 0 1024 1024\" class=\"icon\" version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"#000000\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"><path d=\"M182.52 146.2h585.14v256h73.15V73.06H109.38v877.71h256v-73.14H182.52z\" fill=\"#000000\"></path><path d=\"M255.67 219.34h438.86v73.14H255.67zM255.67 365.63h365.71v73.14H255.67zM255.67 511.91H475.1v73.14H255.67zM775.22 458.24L439.04 794.42l-0.52 154.64 155.68 0.52L930.38 613.4 775.22 458.24z m51.72 155.16l-25.43 25.43-51.73-51.72 25.44-25.44 51.72 51.73z m-77.14 77.15L620.58 819.77l-51.72-51.72 129.22-129.22 51.72 51.72zM511.91 876.16l0.17-51.34 5.06-5.06 51.72 51.72-4.85 4.85-52.1-0.17z\" fill=\"#000000\"></path></g></svg>",
                        $"/Contract/Sign/{contract.Id}"
                    );
                    System.Diagnostics.Debug.WriteLine($"Notification sent to freelancer: {freelancerId}");
                }

                TempData["Message"] = "Contract created successfully! Please review and sign to begin the project. The freelancer has been notified.";
                return RedirectToAction("Details", "ManageProjectClient", new { id = model.ProjectId });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating contract: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                ModelState.AddModelError("", $"Error creating contract: {ex.Message}");
                if (ex.InnerException != null)
                {
                    ModelState.AddModelError("", $"Inner exception: {ex.InnerException.Message}");
                }
                
                model.AvailableTemplates = await GetAvailableTemplatesAsync(model.ProjectCategory);
                return View(model);
            }
        }

        // GET: Contract/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var contract = await _contractService.GetContractByIdAsync(id);
            if (contract == null)
                return NotFound();

            var userId = GetCurrentUserId();
            if (!CanUserAccessContract(contract, userId))
                return Forbid();

            var viewModel = MapToContractViewModel(contract);
            
            // Check for existing termination request
            var existingTermination = await _context.ContractTerminations
                .FirstOrDefaultAsync(ct => ct.ContractId == id && ct.Status != "Cancelled");
            
            if (existingTermination != null)
            {
                ViewData["ExistingTerminationId"] = existingTermination.Id;
                ViewData["HasExistingTermination"] = true;
            }
            else
            {
                ViewData["HasExistingTermination"] = false;
            }
            
            // Log contract view
            await _contractService.LogContractActionAsync(id, userId, "Viewed", "Contract details viewed", GetClientIpAddress(), Request.Headers["User-Agent"]);

            return View(viewModel);
        }

        // GET: Contract/Sign/{id}
        [HttpGet]
        public async Task<IActionResult> Sign(Guid id)
        {
            var contract = await _contractService.GetContractByIdAsync(id);
            if (contract == null)
                return NotFound();

            var userId = GetCurrentUserId();
            if (!CanUserAccessContract(contract, userId))
                return Forbid();

            if (!await _contractService.CanUserSignContractAsync(id, userId))
            {
                TempData["ErrorMessage"] = "You cannot sign this contract at this time.";
                return RedirectToAction("Details", new { id });
            }

            var isClient = contract.Project.UserId == userId;
            var isFreelancer = contract.Bidding.UserId == userId;

            var viewModel = new ContractSigningViewModel
            {
                ContractId = id,
                ContractTitle = contract.ContractTitle,
                ContractContent = contract.ContractContent,
                Status = contract.Status,
                ProjectName = contract.Project.ProjectName,
                ClientName = $"{contract.Project.User.FirstName} {contract.Project.User.LastName}",
                FreelancerName = $"{contract.Bidding.User.FirstName} {contract.Bidding.User.LastName}",
                AgreedAmount = contract.Bidding.Budget,
                DeliveryTimeline = contract.Bidding.Delivery,
                CurrentUserId = userId,
                IsClient = isClient,
                IsFreelancer = isFreelancer,
                CanSign = true
            };

            if (isClient)
            {
                viewModel.HasAlreadySigned = contract.ClientSignedAt.HasValue;
                viewModel.OtherPartyHasSigned = contract.FreelancerSignedAt.HasValue;
                viewModel.OtherPartySignedAt = contract.FreelancerSignedAt?.ToString("MMMM dd, yyyy");
            }
            else
            {
                viewModel.HasAlreadySigned = contract.FreelancerSignedAt.HasValue;
                viewModel.OtherPartyHasSigned = contract.ClientSignedAt.HasValue;
                viewModel.OtherPartySignedAt = contract.ClientSignedAt?.ToString("MMMM dd, yyyy");
            }

            return View(viewModel);
        }

        // POST: Contract/Sign
        [HttpPost]
        public async Task<IActionResult> Sign(ContractSigningViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var userId = GetCurrentUserId();
                var ipAddress = GetClientIpAddress();
                var userAgent = Request.Headers["User-Agent"];

                var contract = await _contractService.SignContractAsync(
                    model.ContractId, 
                    userId, 
                    model.SignatureType!, 
                    model.SignatureData!, 
                    ipAddress, 
                    userAgent!
                );

                // Send notifications
                if (await _contractService.IsContractFullySignedAsync(model.ContractId))
                {
                    // Contract is fully signed - notify both parties
                    await NotifyContractFullySignedAsync(contract);
                    TempData["SuccessMessage"] = "Contract signed successfully! The project is now active.";
                }
                else
                {
                    // Partial signature - notify the other party
                    await NotifyPartialSignatureAsync(contract, userId);
                    TempData["SuccessMessage"] = "Contract signed successfully! Waiting for the other party to sign.";
                }

                return RedirectToAction("Details", new { id = model.ContractId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error signing contract: {ex.Message}");
                return View(model);
            }
        }

        // GET: Contract/Download/{id}
        [HttpGet]
        public async Task<IActionResult> Download(Guid id)
        {
            try
            {
                var contract = await _contractService.GetContractByIdAsync(id);
                if (contract == null)
                    return NotFound("Contract not found");

                var userId = GetCurrentUserId();
                if (!CanUserAccessContract(contract, userId))
                    return Forbid("You are not authorized to access this contract");

                // Validate that PDF data can be generated
                var pdfData = await _contractService.GenerateContractPdfAsync(id);
                
                if (pdfData == null || pdfData.Length == 0)
                {
                    TempData["ErrorMessage"] = "Failed to generate PDF content";
                    return RedirectToAction("Details", new { id });
                }

                // Validate PDF integrity
                var isValid = await _contractService.VerifyDocumentIntegrityAsync(id);
                if (!isValid)
                {
                    TempData["ErrorMessage"] = "Contract document integrity check failed";
                    return RedirectToAction("Details", new { id });
                }
                
                // Log download
                await _contractService.LogContractActionAsync(id, userId, "Downloaded", "Contract PDF downloaded", GetClientIpAddress(), Request.Headers["User-Agent"]);

                var fileName = $"Contract_{contract.Project.ProjectName}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(pdfData, "application/pdf", fileName);
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = $"PDF generation failed: {ex.Message}";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Unexpected error generating PDF: {ex.Message}";
                return RedirectToAction("Details", new { id });
            }
        }

        // GET: Contract/MyContracts
        [HttpGet]
        public async Task<IActionResult> MyContracts(string? status = null)
        {
            var userId = GetCurrentUserId();
            var contracts = await _contractService.GetContractsByUserIdAsync(userId, status);
            
            var viewModels = new List<ContractViewModel>();
            foreach (var contract in contracts)
            {
                viewModels.Add(MapToContractViewModel(contract));
            }

            ViewBag.CurrentStatus = status;
            ViewBag.StatusOptions = new[]
            {
                new { Value = "", Text = "All Contracts" },
                new { Value = "Draft", Text = "Draft" },
                new { Value = "AwaitingFreelancer", Text = "Awaiting Freelancer" },
                new { Value = "AwaitingClient", Text = "Awaiting Client" },
                new { Value = "Active", Text = "Active" },
                new { Value = "Completed", Text = "Completed" }
            };

            return View(viewModels);
        }

        // GET: Contract/GetTemplateContent/{id}
        [HttpGet]
        public async Task<IActionResult> GetTemplateContent(Guid id)
        {
            try
            {
                var template = await _context.ContractTemplates
                    .Where(ct => ct.Id == id && ct.IsActive)
                    .Select(ct => new { ct.Id, ct.Name, ct.TemplateContent })
                    .FirstOrDefaultAsync();

                if (template == null)
                    return NotFound(new { error = "Template not found" });

                return Json(new { 
                    success = true, 
                    templateId = template.Id,
                    templateName = template.Name,
                    templateContent = template.TemplateContent 
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // POST: Contract/MarkComplete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkComplete(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var contract = await _context.Contracts
                    .Include(c => c.Project)
                        .ThenInclude(p => p.User)
                    .Include(c => c.Bidding)
                        .ThenInclude(b => b.User)
                    .FirstOrDefaultAsync(c => c.Id == id);
                
                if (contract == null)
                {
                    TempData["ErrorMessage"] = "Contract not found.";
                    return RedirectToAction("MyContracts");
                }

                // Verify user can access this contract
                if (!CanUserAccessContract(contract, userId))
                {
                    TempData["ErrorMessage"] = "You are not authorized to access this contract.";
                    return RedirectToAction("MyContracts");
                }

                // Verify contract is active
                if (contract.Status != "Active")
                {
                    TempData["ErrorMessage"] = "Only active contracts can be marked as complete.";
                    return RedirectToAction("Details", new { id });
                }

                // Check if there are enough accepted deliverables
                var acceptedDeliverablesCount = await _context.Deliverables
                    .CountAsync(d => d.ContractId == id && d.Status == "Approved");
                
                if (acceptedDeliverablesCount < 1)
                {
                    TempData["ErrorMessage"] = "Project cannot be completed until at least one deliverable has been accepted by the client.";
                    return RedirectToAction("Details", new { id });
                }

                var isClient = contract.Project.UserId == userId;
                var now = DateTime.UtcNow.ToLocalTime();

                // Mark completion based on user role
                if (isClient)
                {
                    if (contract.ClientMarkedCompleteAt.HasValue)
                    {
                        TempData["ErrorMessage"] = "You have already marked this project as complete.";
                        return RedirectToAction("Details", new { id });
                    }
                    contract.ClientMarkedCompleteAt = now;
                }
                else // Freelancer
                {
                    if (contract.FreelancerMarkedCompleteAt.HasValue)
                    {
                        TempData["ErrorMessage"] = "You have already marked this project as complete.";
                        return RedirectToAction("Details", new { id });
                    }
                    contract.FreelancerMarkedCompleteAt = now;
                }

                // Check if both parties have marked complete
                if (contract.ClientMarkedCompleteAt.HasValue && contract.FreelancerMarkedCompleteAt.HasValue)
                {
                    contract.Status = "Completed";
                    contract.CompletedAt = now;
                    
                    // Update project status as well
                    var project = contract.Project;
                    project.Status = "Completed";
                    
                    await _context.SaveChangesAsync();

                    // Send completion notifications to both parties
                    await NotifyProjectCompletedAsync(contract);

                    TempData["SuccessMessage"] = "Project has been successfully completed! Both parties have confirmed completion.";
                }
                else
                {
                    await _context.SaveChangesAsync();
                    
                    // Send notification to the other party
                    await NotifyPartialCompletionAsync(contract, userId, isClient);
                    
                    var otherParty = isClient ? "freelancer" : "client";
                    TempData["SuccessMessage"] = $"You have marked the project as complete. Waiting for the {otherParty} to confirm completion.";
                }

                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error marking project as complete: {ex.Message}";
                return RedirectToAction("Details", new { id });
            }
        }





        #region Helper Methods

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        private string GetClientIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        private bool CanUserAccessContract(Contract contract, string userId)
        {
            return contract.Project.UserId == userId || contract.Bidding.UserId == userId;
        }

        private async Task<List<ContractTemplateOption>> GetAvailableTemplatesAsync(string? category)
        {
            var templates = await _context.ContractTemplates
                .Where(ct => ct.IsActive)
                .Select(ct => new ContractTemplateOption
                {
                    Id = ct.Id,
                    Name = ct.Name,
                    Description = ct.Description,
                    Category = ct.Category,
                    PreviewImagePath = ct.PreviewImagePath
                })
                .ToListAsync();

            // Order templates: exact category match first, then others
            return templates
                .OrderBy(ct => !string.IsNullOrEmpty(category) && 
                              ct.Category?.ToLower() == category.ToLower() ? 0 : 1)
                .ThenBy(ct => ct.Name)
                .ToList();
        }

        private async Task<string> GetFreelancerIdFromProjectAsync(Guid projectId)
        {
            var project = await _context.Projects
                .Include(p => p.AcceptedBid)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            return project?.AcceptedBid?.UserId ?? string.Empty;
        }

        private async Task UpdateContractTermsAsync(Guid contractId, CreateContractViewModel model)
        {
            var contract = await _context.Contracts.FindAsync(contractId);
            if (contract == null) return;

            // Update payment terms
            var paymentTerms = new
            {
                upfront = model.UpfrontPercentage,
                final = model.FinalPercentage,
                milestones = model.Milestones.Select(m => new
                {
                    name = m.Name,
                    description = m.Description,
                    percentage = m.Percentage,
                    dueDate = m.DueDate
                }).ToList()
            };
            var paymentTermsJson = JsonSerializer.Serialize(paymentTerms);
            contract.PaymentTerms = paymentTermsJson;

            // Update revision policy
            var revisionPolicy = new
            {
                freeRevisions = model.FreeRevisions,
                additionalCost = model.AdditionalRevisionCost,
                scope = model.RevisionScope
            };
            var revisionPolicyJson = JsonSerializer.Serialize(revisionPolicy);
            contract.RevisionPolicy = revisionPolicyJson;

            // Update timeline
            var timeline = new
            {
                startDate = model.StartDate.ToString("MMMM dd, yyyy"),
                deadline = model.Deadline.ToString("MMMM dd, yyyy"),
                milestones = model.Milestones.Where(m => m.DueDate.HasValue).Select(m => new
                {
                    name = m.Name,
                    dueDate = m.DueDate.Value.ToString("MMMM dd, yyyy")
                }).ToList()
            };
            var timelineJson = JsonSerializer.Serialize(timeline);
            contract.Timeline = timelineJson;

            // Update deliverable requirements
            contract.DeliverableRequirements = JsonSerializer.Serialize(model.DeliverableRequirements);

            await _context.SaveChangesAsync();

            // Update the contract content with the actual terms
            await _contractService.UpdateContractContentWithTermsAsync(contractId, paymentTermsJson, revisionPolicyJson, timelineJson);
        }

        private ContractViewModel MapToContractViewModel(Contract contract)
        {
            // Get accepted deliverables count
            var acceptedDeliverablesCount = _context.Deliverables
                .Where(d => d.ContractId == contract.Id && d.Status == "Approved")
                .Count();

            var viewModel = new ContractViewModel
            {
                Id = contract.Id,
                ContractTitle = contract.ContractTitle,
                ContractContent = contract.ContractContent,
                Status = contract.Status,
                CreatedAt = contract.CreatedAt,
                LastModifiedAt = contract.LastModifiedAt,
                ProjectId = contract.ProjectId,
                ProjectName = contract.Project.ProjectName,
                ProjectDescription = contract.Project.ProjectDescription,
                ProjectBudget = contract.Project.Budget,
                ProjectCategory = contract.Project.Category,
                ClientId = contract.Project.UserId,
                ClientName = $"{contract.Project.User.FirstName} {contract.Project.User.LastName}",
                ClientEmail = contract.Project.User.Email,
                ClientPhoto = contract.Project.User.Photo,
                ClientSignedAt = contract.ClientSignedAt,
                ClientSignatureData = contract.ClientSignatureData,
                ClientSignatureType = contract.ClientSignatureType,
                FreelancerId = contract.Bidding.UserId,
                FreelancerName = $"{contract.Bidding.User.FirstName} {contract.Bidding.User.LastName}",
                FreelancerEmail = contract.Bidding.User.Email,
                FreelancerPhoto = contract.Bidding.User.Photo,
                FreelancerSignedAt = contract.FreelancerSignedAt,
                FreelancerSignatureData = contract.FreelancerSignatureData,
                FreelancerSignatureType = contract.FreelancerSignatureType,
                AgreedAmount = contract.Bidding.Budget,
                DeliveryTimeline = contract.Bidding.Delivery,
                Proposal = contract.Bidding.Proposal,
                DocumentPath = contract.DocumentPath,
                // Completion tracking
                ClientMarkedCompleteAt = contract.ClientMarkedCompleteAt,
                FreelancerMarkedCompleteAt = contract.FreelancerMarkedCompleteAt,
                CompletedAt = contract.CompletedAt,
                AcceptedDeliverablesCount = acceptedDeliverablesCount
            };

            // Parse JSON fields
            if (!string.IsNullOrEmpty(contract.PaymentTerms))
            {
                try
                {
                    var paymentTermsJson = JsonSerializer.Deserialize<JsonElement>(contract.PaymentTerms);
                    viewModel.PaymentTerms = new PaymentTermsViewModel
                    {
                        UpfrontPercentage = paymentTermsJson.GetProperty("upfront").GetInt32(),
                        FinalPercentage = paymentTermsJson.GetProperty("final").GetInt32()
                    };
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(contract.DeliverableRequirements))
            {
                try
                {
                    viewModel.DeliverableRequirements = JsonSerializer.Deserialize<List<string>>(contract.DeliverableRequirements);
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(contract.Timeline))
            {
                try
                {
                    var timelineJson = JsonSerializer.Deserialize<JsonElement>(contract.Timeline);
                    var startDateStr = timelineJson.GetProperty("startDate").GetString();
                    var deadlineStr = timelineJson.GetProperty("deadline").GetString();
                    
                    // Parse the formatted date strings
                    if (DateTime.TryParse(startDateStr, out DateTime startDate) && 
                        DateTime.TryParse(deadlineStr, out DateTime deadline))
                    {
                        viewModel.Timeline = new TimelineViewModel
                        {
                            StartDate = startDate,
                            Deadline = deadline
                        };
                    }
                }
                catch { }
            }

            // Map audit logs
            viewModel.AuditLogs = contract.AuditLogs?.Select(al => new ContractAuditLogViewModel
            {
                Id = al.Id,
                UserName = $"{al.User.FirstName} {al.User.LastName}",
                Action = al.Action,
                Details = al.Details,
                Timestamp = al.Timestamp,
                IPAddress = al.IPAddress
            }).ToList() ?? new List<ContractAuditLogViewModel>();

            return viewModel;
        }

        private async Task NotifyContractFullySignedAsync(Contract contract)
        {
            var clientMessage = $"Contract for '{contract.Project.ProjectName}' has been fully signed. The project is now active!";
            var freelancerMessage = $"Contract for '{contract.Project.ProjectName}' has been fully signed. You can now start working on the project!";

            await _notificationService.CreateNotificationAsync(
                contract.Project.UserId,
                "Contract Fully Signed",
                clientMessage,
                "Contract",
                "<svg viewBox=\"0 0 1024 1024\" class=\"icon\" version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"#000000\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"><path d=\"M182.52 146.2h585.14v402.28h73.15V73.06H109.38v877.71h402.28v-73.14H182.52z\" fill=\"#000000\"></path><path d=\"M255.67 219.34h438.86v73.14H255.67zM255.67 365.63h365.71v73.14H255.67zM255.67 511.91H475.1v73.14H255.67zM731.02 585.06c-100.99 0-182.86 81.87-182.86 182.86s81.87 182.86 182.86 182.86 182.86-81.87 182.86-182.86-81.87-182.86-182.86-182.86z m0 292.57c-60.5 0-109.71-49.22-109.71-109.71 0-60.5 49.22-109.71 109.71-109.71 60.5 0 109.71 49.22 109.71 109.71 0 60.49-49.22 109.71-109.71 109.71z\" fill=\"#000000\"></path><path d=\"M717.88 777.65l-42.55-38.13-36.61 40.86 84.02 75.27 102.98-118.47-41.39-36z\" fill=\"#000000\"></path></g></svg>",
                $"/Contract/Details/{contract.Id}"
            );

            await _notificationService.CreateNotificationAsync(
                contract.Bidding.UserId,
                "Contract Fully Signed",
                freelancerMessage,
                "Contract",
                "<svg viewBox=\"0 0 1024 1024\" class=\"icon\" version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"#000000\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"><path d=\"M182.52 146.2h585.14v402.28h73.15V73.06H109.38v877.71h402.28v-73.14H182.52z\" fill=\"#000000\"></path><path d=\"M255.67 219.34h438.86v73.14H255.67zM255.67 365.63h365.71v73.14H255.67zM255.67 511.91H475.1v73.14H255.67zM731.02 585.06c-100.99 0-182.86 81.87-182.86 182.86s81.87 182.86 182.86 182.86 182.86-81.87 182.86-182.86-81.87-182.86-182.86-182.86z m0 292.57c-60.5 0-109.71-49.22-109.71-109.71 0-60.5 49.22-109.71 109.71-109.71 60.5 0 109.71 49.22 109.71 109.71 0 60.49-49.22 109.71-109.71 109.71z\" fill=\"#000000\"></path><path d=\"M717.88 777.65l-42.55-38.13-36.61 40.86 84.02 75.27 102.98-118.47-41.39-36z\" fill=\"#000000\"></path></g></svg>",
                $"/Contract/Details/{contract.Id}"
            );
        }

        private async Task NotifyPartialSignatureAsync(Contract contract, string signerUserId)
        {
            var isClientSigner = contract.Project.UserId == signerUserId;
            var recipientId = isClientSigner ? contract.Bidding.UserId : contract.Project.UserId;
            var signerName = isClientSigner ? 
                $"{contract.Project.User.FirstName} {contract.Project.User.LastName}" : 
                $"{contract.Bidding.User.FirstName} {contract.Bidding.User.LastName}";

            var message = $"{signerName} has signed the contract for '{contract.Project.ProjectName}'. Please review and sign to activate the project.";

            await _notificationService.CreateNotificationAsync(
                recipientId,
                "Contract Signature Required",
                message,
                "Contract",
                "<svg viewBox=\"0 0 1024 1024\" class=\"icon\" version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"#000000\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"><path d=\"M182.99 146.2h585.14v402.29h73.14V73.06H109.84v877.71H512v-73.14H182.99z\" fill=\"#000000\"></path><path d=\"M256.13 219.34h438.86v73.14H256.13zM256.13 365.63h365.71v73.14H256.13zM256.13 511.91h219.43v73.14H256.13zM731.55 585.06c-100.99 0-182.86 81.87-182.86 182.86s81.87 182.86 182.86 182.86c100.99 0 182.86-81.87 182.86-182.86s-81.86-182.86-182.86-182.86z m0 292.57c-60.5 0-109.71-49.22-109.71-109.71 0-60.5 49.22-109.71 109.71-109.71 60.5 0 109.71 49.22 109.71 109.71 0.01 60.49-49.21 109.71-109.71 109.71z\" fill=\"#000000\"></path><path d=\"M758.99 692.08h-54.86v87.27l69.39 68.76 38.61-38.96-53.14-52.66z\" fill=\"#000000\"></path></g></svg>",
                $"/Contract/Sign/{contract.Id}"
            );
        }

        private async Task NotifyProjectCompletedAsync(Contract contract)
        {
            var clientMessage = $"Project '{contract.Project.ProjectName}' has been completed successfully! Both parties have confirmed completion.";
            var freelancerMessage = $"Project '{contract.Project.ProjectName}' has been completed successfully! Both parties have confirmed completion.";

            await _notificationService.CreateNotificationAsync(
                contract.Project.UserId,
                "Project Completed",
                clientMessage,
                "project_completed",
                "<svg fill=\"#000000\" viewBox=\"0 0 24 24\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"><path d=\"M4,1A1,1,0,0,0,3,2V22a1,1,0,0,0,2,0V17H20a1,1,0,0,0,1-1V4a1,1,0,0,0-1-1H5V2A1,1,0,0,0,4,1ZM7,15H5V13H7Zm0-4H5V9H7ZM17,5h2V7H17Zm0,4h2v2H17Zm0,4h2v2H17ZM13,5h2V7H13Zm0,4h2v2H13Zm0,4h2v2H13ZM9,5h2V7H9ZM9,9h2v2H9Zm0,4h2v2H9ZM7,5V7H5V5Z\"></path></g></svg>",
                $"/Contract/Details/{contract.Id}"
            );

            await _notificationService.CreateNotificationAsync(
                contract.Bidding.UserId,
                "Project Completed",
                freelancerMessage,
                "project_completed",
                "<svg fill=\"#000000\" viewBox=\"0 0 24 24\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"><path d=\"M4,1A1,1,0,0,0,3,2V22a1,1,0,0,0,2,0V17H20a1,1,0,0,0,1-1V4a1,1,0,0,0-1-1H5V2A1,1,0,0,0,4,1ZM7,15H5V13H7Zm0-4H5V9H7ZM17,5h2V7H17Zm0,4h2v2H17Zm0,4h2v2H17ZM13,5h2V7H13Zm0,4h2v2H13Zm0,4h2v2H13ZM9,5h2V7H9ZM9,9h2v2H9Zm0,4h2v2H9ZM7,5V7H5V5Z\"></path></g></svg>",
                $"/Contract/Details/{contract.Id}"
            );
        }

        private async Task NotifyPartialCompletionAsync(Contract contract, string completerId, bool isClientCompleter)
        {
            var recipientId = isClientCompleter ? contract.Bidding.UserId : contract.Project.UserId;
            var completerName = isClientCompleter ? 
                $"{contract.Project.User.FirstName} {contract.Project.User.LastName}" : 
                $"{contract.Bidding.User.FirstName} {contract.Bidding.User.LastName}";

            var message = $"{completerName} has marked project '{contract.Project.ProjectName}' as complete. Please review and confirm completion to finalize the project.";

            await _notificationService.CreateNotificationAsync(
                recipientId,
                "Project Completion Confirmation Required",
                message,
                "project_completed",
                "<svg fill=\"#000000\" viewBox=\"0 0 24 24\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"><path d=\"M4,1A1,1,0,0,0,3,2V22a1,1,0,0,0,2,0V17H20a1,1,0,0,0,1-1V4a1,1,0,0,0-1-1H5V2A1,1,0,0,0,4,1ZM7,15H5V13H7Zm0-4H5V9H7ZM17,5h2V7H17Zm0,4h2v2H17Zm0,4h2v2H17ZM13,5h2V7H13Zm0,4h2v2H13Zm0,4h2v2H13ZM9,5h2V7H9ZM9,9h2v2H9Zm0,4h2v2H9ZM7,5V7H5V5Z\"></path></g></svg>",
                $"/Contract/Details/{contract.Id}"
            );
        }

        #endregion
    }
}
