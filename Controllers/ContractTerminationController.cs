using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using Freelancing.Services;
using System.Security.Claims;
using System;

namespace Freelancing.Controllers
{
    [Authorize]
    public class ContractTerminationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IContractTerminationService _terminationService;
        private readonly INotificationService _notificationService;

        public ContractTerminationController(ApplicationDbContext context, IContractTerminationService terminationService, INotificationService notificationService)
        {
            _context = context;
            _terminationService = terminationService;
            _notificationService = notificationService;
        }

        // GET: ContractTermination/Create/{contractId}
        [HttpGet]
        public async Task<IActionResult> Create(Guid contractId)
        {
            var contract = await _context.Contracts
                .Include(c => c.Project)
                    .ThenInclude(p => p.User)
                .Include(c => c.Bidding)
                    .ThenInclude(b => b.User)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                return NotFound("Contract not found");

            var userId = GetCurrentUserId();
            if (!CanUserAccessContract(contract, userId))
                return Forbid("You are not authorized to terminate this contract");

            if (contract.Status != "Active")
            {
                TempData["ErrorMessage"] = "Only active contracts can be terminated.";
                return RedirectToAction("Details", "Contract", new { id = contractId });
            }

            var isClient = contract.Project.UserId == userId;
            var isFreelancer = contract.Bidding.UserId == userId;

            var viewModel = new CreateTerminationViewModel
            {
                ContractId = contractId,
                ContractTitle = contract.ContractTitle,
                ProjectName = contract.Project.ProjectName,
                ClientName = $"{contract.Project.User.FirstName} {contract.Project.User.LastName}",
                FreelancerName = $"{contract.Bidding.User.FirstName} {contract.Bidding.User.LastName}",
                AgreedAmount = contract.Bidding.Budget,
                ContractStatus = contract.Status,
                IsClient = isClient,
                IsFreelancer = isFreelancer,

                FinalPayment = contract.Bidding.Budget * 0.5m,
                TerminationReason = "",
                TerminationDetails = "",
                SettlementNotes = "",
                UnderstandImplications = false,
                AgreeToTerms = false
            };

            return View(viewModel);
        }

        // POST: ContractTermination/Create
        [HttpPost]
        public async Task<IActionResult> Create(CreateTerminationViewModel model)
        {
            // Debug: Log the model state
            if (!ModelState.IsValid)
            {
                // Repopulate the model with contract details before returning the view
                var contract = await _context.Contracts
                    .Include(c => c.Project)
                        .ThenInclude(p => p.User)
                    .Include(c => c.Bidding)
                        .ThenInclude(b => b.User)
                    .FirstOrDefaultAsync(c => c.Id == model.ContractId);

                if (contract != null)
                {
                    var userId = GetCurrentUserId();
                    var isClient = contract.Project.UserId == userId;
                    var isFreelancer = contract.Bidding.UserId == userId;

                    model.ContractTitle = contract.ContractTitle;
                    model.ProjectName = contract.Project.ProjectName;
                    model.ClientName = $"{contract.Project.User.FirstName} {contract.Project.User.LastName}";
                    model.FreelancerName = $"{contract.Bidding.User.FirstName} {contract.Bidding.User.LastName}";
                    model.AgreedAmount = contract.Bidding.Budget;
                    model.ContractStatus = contract.Status;
                    model.IsClient = isClient;
                    model.IsFreelancer = isFreelancer;
                }

                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                TempData["ErrorMessage"] = $"Validation errors: {string.Join(", ", errors)}";
                return View(model);
            }

            try
            {
                var userId = GetCurrentUserId();
                
                // Debug: Log the values being passed
                TempData["DebugInfo"] = $"ContractId: {model.ContractId}, Reason: {model.TerminationReason?.Length ?? 0} chars, Details: {model.TerminationDetails?.Length ?? 0} chars, FinalPayment: {model.FinalPayment}";
                
                var termination = await _terminationService.CreateTerminationRequestAsync(
                    model.ContractId,
                    userId,
                    model.TerminationReason ?? "",
                    model.TerminationDetails ?? "",
                    model.FinalPayment,
                    model.SettlementNotes
                );

                // Send notification to the other party
                var contract = await _context.Contracts
                    .Include(c => c.Project)
                        .ThenInclude(p => p.User)
                    .Include(c => c.Bidding)
                        .ThenInclude(b => b.User)
                    .FirstOrDefaultAsync(c => c.Id == model.ContractId);

                if (contract != null)
                {
                    var isClient = contract.Project.UserId == userId;
                    var otherPartyUserId = isClient ? contract.Bidding.UserId : contract.Project.UserId;
                    var requestorName = isClient ? $"{contract.Project.User.FirstName} {contract.Project.User.LastName}" : $"{contract.Bidding.User.FirstName} {contract.Bidding.User.LastName}";

                    var terminationIconSvg = "<svg viewBox=\"0 0 1024 1024\" class=\"icon\" version=\"1.1\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"#000000\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"><path d=\"M183 146.2h585.15v402.28h73.14V73.06H109.86v877.71h402.16v-73.14H183z\" fill=\"#000000\"></path><path d=\"M256.14 219.34H695v73.14H256.14zM256.14 365.63h365.71v73.14H256.14zM256.14 511.91h219.43v73.14H256.14zM731.57 585.06c-100.99 0-182.86 81.87-182.86 182.86s81.87 182.86 182.86 182.86 182.86-81.87 182.86-182.86-81.87-182.86-182.86-182.86z m0 292.57c-60.5 0-109.71-49.22-109.71-109.71 0-60.5 49.22-109.71 109.71-109.71 60.5 0 109.71 49.22 109.71 109.71 0 60.49-49.21 109.71-109.71 109.71z\" fill=\"#000000\"></path><path d=\"M658.16 740.48h146.29v54.86H658.16z\" fill=\"#000000\"></path></g></svg>";

                    await _notificationService.CreateNotificationAsync(
                        userId: otherPartyUserId,
                        title: "Contract Termination Request",
                        message: $"{requestorName} has requested to terminate the contract for project '{contract.Project.ProjectName}'. Please review and sign the termination agreement.",
                        type: "contract_termination",
                        iconSvg: terminationIconSvg,
                        relatedUrl: $"/ContractTermination/Details/{termination.Id}"
                    );
                }

                TempData["SuccessMessage"] = "Termination request created successfully! The other party has been notified.";
                return RedirectToAction("Details", new { id = termination.Id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating termination request: {ex.Message}");
                TempData["ErrorMessage"] = $"Exception: {ex.Message}";
                return View(model);
            }
        }

        // GET: ContractTermination/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var termination = await _terminationService.GetTerminationByIdAsync(id);
            if (termination == null)
                return NotFound();

            var userId = GetCurrentUserId();
            if (!CanUserAccessTermination(termination, userId))
                return Forbid();

            var viewModel = MapToTerminationViewModel(termination, userId);
            return View(viewModel);
        }

        // GET: ContractTermination/Sign/{id}
        [HttpGet]
        public async Task<IActionResult> Sign(Guid id)
        {
            var termination = await _terminationService.GetTerminationByIdAsync(id);
            if (termination == null)
                return NotFound();

            var userId = GetCurrentUserId();
            if (!CanUserAccessTermination(termination, userId))
                return Forbid();

            if (!await _terminationService.CanUserSignTerminationAsync(id, userId))
            {
                TempData["ErrorMessage"] = "You cannot sign this termination request at this time.";
                return RedirectToAction("Details", new { id });
            }

            var isClient = termination.Contract.Project.UserId == userId;
            var isFreelancer = termination.Contract.Bidding.UserId == userId;

            var viewModel = new TerminationSigningViewModel
            {
                TerminationId = id,
                ContractId = termination.ContractId,
                TerminationTitle = $"Termination Request - {termination.Contract.Project.ProjectName}",
                ProjectName = termination.Contract.Project.ProjectName,
                ClientName = $"{termination.Contract.Project.User.FirstName} {termination.Contract.Project.User.LastName}",
                FreelancerName = $"{termination.Contract.Bidding.User.FirstName} {termination.Contract.Bidding.User.LastName}",
                TerminationReason = termination.TerminationReason,
                TerminationDetails = termination.TerminationDetails,
                FinalPayment = termination.FinalPayment,
                SettlementNotes = termination.SettlementNotes,
                Status = termination.Status,
                RequestedAt = termination.RequestedAt,
                RequestedByRole = termination.RequestedByUserRole,
                CurrentUserId = userId,
                IsClient = isClient,
                IsFreelancer = isFreelancer,
                CanSign = true
            };

            if (isClient)
            {
                viewModel.HasAlreadySigned = termination.ClientSignedAt.HasValue;
                viewModel.OtherPartyHasSigned = termination.FreelancerSignedAt.HasValue;
                viewModel.OtherPartySignedAt = termination.FreelancerSignedAt?.ToString("MMMM dd, yyyy");
            }
            else
            {
                viewModel.HasAlreadySigned = termination.FreelancerSignedAt.HasValue;
                viewModel.OtherPartyHasSigned = termination.ClientSignedAt.HasValue;
                viewModel.OtherPartySignedAt = termination.ClientSignedAt?.ToString("MMMM dd, yyyy");
            }

            return View(viewModel);
        }

        // POST: ContractTermination/Sign
        [HttpPost]
        public async Task<IActionResult> Sign(TerminationSigningViewModel model)
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

                var termination = await _terminationService.SignTerminationAsync(
                    model.TerminationId,
                    userId,
                    model.SignatureType!,
                    model.SignatureData!,
                    ipAddress,
                    userAgent!
                );

                if (await _terminationService.IsTerminationFullySignedAsync(model.TerminationId))
                {
                    TempData["SuccessMessage"] = "Termination signed successfully! The contract will be terminated.";
                }
                else
                {
                    TempData["SuccessMessage"] = "Termination signed successfully! Waiting for the other party to sign.";
                }

                return RedirectToAction("Details", new { id = model.TerminationId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error signing termination: {ex.Message}");
                return View(model);
            }
        }

        // POST: ContractTermination/Execute
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Execute(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var termination = await _terminationService.GetTerminationByIdAsync(id);

                if (termination == null)
                {
                    TempData["ErrorMessage"] = "Termination request not found.";
                    return RedirectToAction("Index", "Contract");
                }

                // Verify user can access this termination
                if (!CanUserAccessTermination(termination, userId))
                {
                    TempData["ErrorMessage"] = "You are not authorized to execute this termination.";
                    return RedirectToAction("Details", new { id });
                }

                // Verify termination is fully signed
                if (termination.Status != "Signed")
                {
                    TempData["ErrorMessage"] = "Termination cannot be executed. Both parties must sign the agreement first.";
                    return RedirectToAction("Details", new { id });
                }

                // Execute the termination
                await _terminationService.ExecuteTerminationAsync(id, userId);

                // Update contract status to terminated
                var contract = await _context.Contracts
                    .Include(c => c.Project)
                    .FirstOrDefaultAsync(c => c.Id == termination.ContractId);

                if (contract != null)
                {
                    contract.Status = "Terminated";
                    contract.TerminatedAt = DateTime.UtcNow;

                    // Update project status back to "Open" when contract is terminated
                    if (contract.Project != null)
                    {
                        contract.Project.Status = "Open";
                        contract.Project.AcceptedBidId = null; // Clear the accepted bid
                    }

                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Contract has been successfully terminated! The project is now open for new bids.";
                return RedirectToAction("Details", "Contract", new { id = termination.ContractId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error executing termination: {ex.Message}";
                return RedirectToAction("Details", new { id });
            }
        }

        // POST: ContractTermination/Cancel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var termination = await _terminationService.GetTerminationByIdAsync(id);
                
                if (termination == null)
                {
                    TempData["ErrorMessage"] = "Termination request not found.";
                    return RedirectToAction("Index", "Contract");
                }

                // Verify user can cancel this termination (only the requester can cancel)
                if (termination.RequestedByUserId != userId)
                {
                    TempData["ErrorMessage"] = "Only the person who requested the termination can cancel it.";
                    return RedirectToAction("Details", new { id });
                }

                var status = termination.Status ?? string.Empty;
                var cancellable = status.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("AwaitingFreelancer", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("AwaitingClient", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("Awaiting Freelancer", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("Awaiting Client", StringComparison.OrdinalIgnoreCase);

                if (!cancellable)
                {
                    TempData["ErrorMessage"] = "This termination request cannot be cancelled at this time.";
                    return RedirectToAction("Details", new { id });
                }

                // Cancel the termination
                await _terminationService.CancelTerminationAsync(id, userId);

                TempData["SuccessMessage"] = "Termination request has been cancelled.";
                return RedirectToAction("Details", "Contract", new { id = termination.ContractId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error cancelling termination: {ex.Message}";
                return RedirectToAction("Details", new { id });
            }
        }

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

        private bool CanUserAccessTermination(ContractTermination termination, string userId)
        {
            return termination.Contract.Project.UserId == userId || termination.Contract.Bidding.UserId == userId;
        }

        private TerminationViewModel MapToTerminationViewModel(ContractTermination termination, string userId)
        {
            var status = termination.Status ?? string.Empty;
            bool isCancellable = termination.RequestedByUserId == userId && (
                status.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                || status.Equals("AwaitingFreelancer", StringComparison.OrdinalIgnoreCase)
                || status.Equals("AwaitingClient", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Awaiting Freelancer", StringComparison.OrdinalIgnoreCase)
                || status.Equals("Awaiting Client", StringComparison.OrdinalIgnoreCase)
            );

            return new TerminationViewModel
            {
                Id = termination.Id,
                ContractId = termination.ContractId,
                ContractTitle = termination.Contract.ContractTitle,
                ProjectName = termination.Contract.Project.ProjectName,
                ClientName = $"{termination.Contract.Project.User.FirstName} {termination.Contract.Project.User.LastName}",
                FreelancerName = $"{termination.Contract.Bidding.User.FirstName} {termination.Contract.Bidding.User.LastName}",
                AgreedAmount = termination.Contract.Bidding.Budget,
                TerminationReason = termination.TerminationReason,
                TerminationDetails = termination.TerminationDetails,
                FinalPayment = termination.FinalPayment,
                SettlementNotes = termination.SettlementNotes,
                Status = termination.Status,
                RequestedAt = termination.RequestedAt,
                RequestedByRole = termination.RequestedByUserRole,
                RequestedByName = termination.RequestedByUserRole == "Client" ? 
                    $"{termination.Contract.Project.User.FirstName} {termination.Contract.Project.User.LastName}" :
                    $"{termination.Contract.Bidding.User.FirstName} {termination.Contract.Bidding.User.LastName}",
                ClientHasSigned = termination.ClientSignedAt.HasValue,
                ClientSignedAt = termination.ClientSignedAt,
                FreelancerHasSigned = termination.FreelancerSignedAt.HasValue,
                FreelancerSignedAt = termination.FreelancerSignedAt,
                HasSignedDocument = !string.IsNullOrEmpty(termination.DocumentPath),
                DocumentPath = termination.DocumentPath,
                ClientId = termination.Contract.Project.UserId,
                FreelancerId = termination.Contract.Bidding.UserId,
                RequestedByUserId = termination.RequestedByUserId,
                CanUserSign = _terminationService.CanUserSignTerminationAsync(termination.Id, userId).Result,
                CanUserCancel = isCancellable,
                CanUserDownload = !string.IsNullOrEmpty(termination.DocumentPath),
                CanUserExecute = termination.RequestedByUserId == userId && termination.Status == "Signed"
            };
        }
    }
}
