using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using Freelancing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Freelancing.Controllers
{
    [Authorize]
    public class MentorshipMatchingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMentorshipMatchingService _matchingService;
        private readonly INotificationService _notificationService;

        public MentorshipMatchingController(ApplicationDbContext context, IMentorshipMatchingService matchingService, INotificationService notificationService)
        {
            _context = context;
            _matchingService = matchingService;
            _notificationService = notificationService;
        }

        // Main matching page - shows potential matches and existing matches
        public async Task<IActionResult> AvailableMentors()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var userAccount = await _context.UserAccounts
                .FirstOrDefaultAsync(ua => ua.Id == userId);

            if (userAccount == null)
                return Unauthorized();

            var mentorship = await _context.PeerMentorships
                .FirstOrDefaultAsync(pm => pm.UserId == userId);

            if (mentorship == null)
            {
                return RedirectToAction("Registration", "PeerMentorship");
            }

            if (mentorship.Role.ToLower() != "mentee")
            {
                return RedirectToAction("MentorDashboard", "MentorshipMatching", new { Id = mentorship.Id });
            }

            // Get user's skills
            var userSkills = await _context.UserAccountSkills
                .Where(uas => uas.UserAccountId == userId)
                .Include(uas => uas.UserSkill)
                .Select(uas => uas.UserSkill)
                .ToListAsync();

            List<UserAccount> potentialMatches = new();

            if (userSkills.Any())
            {
                // Find potential matches based on role
                if (mentorship.Role.ToLower() == "mentee")
                {
                    potentialMatches = await _matchingService.FindPotentialMentorsAsync(userId);
                }
            }

            var viewModel = new MentorshipMatchingViewModel
            {
                PotentialMatches = potentialMatches,
                UserRole = mentorship.Role,
                TotalSkills = userSkills.Count,
                UserSkills = userSkills,
                HasSkills = userSkills.Any()
            };

            return View(viewModel);
        }
        [HttpGet]
        public async Task<IActionResult> CreateRequest(string id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Get the potential mentor/partner
            var partner = await _context.UserAccounts
                .Include(ua => ua.UserAccountSkills)
                .ThenInclude(uas => uas.UserSkill)
                .FirstOrDefaultAsync(ua => ua.Id == id);

            if (partner == null)
                return NotFound();

            // Check if a request already exists (including declined ones)
            var existingMatch = await _context.MentorshipMatches
                .FirstOrDefaultAsync(mm => mm.MenteeId == userId &&
                                          mm.MentorId == id &&
                                          (mm.Status == "Pending" || mm.Status == "Active" || mm.Status == "Declined"));

            // Check if mentee can re-request after a declined request
            bool canReRequest = false;
            DateTime? nextRequestDate = null;

            if (existingMatch != null && existingMatch.Status == "Declined")
            {
                var daysSinceDecline = (DateTime.UtcNow.ToLocalTime() - existingMatch.DeclinedDate.GetValueOrDefault()).TotalDays;
                canReRequest = daysSinceDecline >= 7; // Allow re-request after 7 days

                if (!canReRequest)
                {
                    nextRequestDate = existingMatch.DeclinedDate.GetValueOrDefault().AddDays(7);
                }
            }

            // Get current user's skills
            var userSkills = await _context.UserAccountSkills
                .Where(uas => uas.UserAccountId == userId)
                .Include(uas => uas.UserSkill)
                .Select(uas => uas.UserSkill)
                .ToListAsync();

            // Get potential matches/prospects (optional - for showing alternatives)
            var prospects = await _context.UserAccounts
                .Where(ua => ua.Id != userId && ua.Id != id)
                .Include(ua => ua.UserAccountSkills)
                .ThenInclude(uas => uas.UserSkill)
                .Take(5)
                .ToListAsync();

            var model = new CreateMatchRequest
            {
                PartnerId = id,
                UserSkills = userSkills,
                Prospect = prospects,
                Partner = partner
            };

            // Set ViewBag properties based on existing request status
            if (existingMatch != null)
            {
                ViewBag.ExistingRequest = true;
                ViewBag.RequestStatus = existingMatch.Status;
                ViewBag.RequestDate = existingMatch.MatchedDate;
                ViewBag.RequestNotes = existingMatch.Notes;
                ViewBag.CanReRequest = canReRequest;
                ViewBag.PartnerId = id; // For ViewMentorship link

                // Set appropriate message based on status
                if (existingMatch.Status == "Pending")
                {
                    ViewBag.StatusMessage = "You have already sent a mentorship request to this mentor. Please wait for their response.";
                    ViewBag.StatusClass = "text-yellow-800 border-yellow-300 bg-yellow-50";
                }
                else if (existingMatch.Status == "Active")
                {
                    ViewBag.StatusMessage = "You already have an active mentorship with this mentor.";
                    ViewBag.StatusClass = "text-green-800 border-green-300 bg-green-50";
                }
                else if (existingMatch.Status == "Declined")
                {
                    if (canReRequest)
                    {
                        ViewBag.StatusMessage = "Your previous request was declined, but you can now send a new request.";
                        ViewBag.StatusClass = "text-blue-800 border-blue-300 bg-blue-50";
                        ViewBag.ExistingRequest = false; // Allow form to show
                    }
                    else
                    {
                        var daysRemaining = Math.Ceiling(7 - (DateTime.UtcNow.ToLocalTime() - existingMatch.DeclinedDate.GetValueOrDefault()).TotalDays);
                        var hoursRemaining = Math.Ceiling((nextRequestDate.Value - DateTime.UtcNow.ToLocalTime()).TotalHours);

                        // More precise messaging based on time remaining
                        if (daysRemaining > 1)
                        {
                            ViewBag.StatusMessage = $"Your request was declined. You can send a new request in {daysRemaining} days.";
                        }
                        else if (hoursRemaining > 1)
                        {
                            ViewBag.StatusMessage = $"Your request was declined. You can send a new request in {hoursRemaining} hours.";
                        }
                        else
                        {
                            ViewBag.StatusMessage = "Your request was declined. You can send a new request very soon.";
                        }

                        ViewBag.StatusClass = "text-red-800 border-red-300 bg-red-50";
                        ViewBag.DaysRemaining = (int)daysRemaining;
                        ViewBag.NextRequestDate = nextRequestDate?.ToString("MMM dd, yyyy 'at' h:mm tt");
                    }
                }
            }

            return View(model);
        }

        // Enhanced POST method with better validation and messaging
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRequest(CreateMatchRequest model)
        {
            if (!ModelState.IsValid)
            {
                // Reload the partner and user skills if validation fails
                await ReloadCreateRequestModel(model);
                return View(model);
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Check for existing matches
            var existingMatch = await _context.MentorshipMatches
                .Include(mm => mm.Mentor) // Include mentor for better messaging
                .FirstOrDefaultAsync(mm => mm.MenteeId == userId &&
                                          mm.MentorId == model.PartnerId);

            // Handle different scenarios
            if (existingMatch != null)
            {
                // If pending or active, don't allow new request
                if (existingMatch.Status == "Pending" || existingMatch.Status == "Active")
                {
                    string message = existingMatch.Status == "Pending"
                        ? "You already have a pending request with this mentor."
                        : "You already have an active mentorship with this mentor.";
                    TempData["Error"] = message;
                    await ReloadCreateRequestModel(model);
                    return View(model);
                }

                // If declined, check if a week has passed
                if (existingMatch.Status == "Declined")
                {
                    var daysSinceDecline = (DateTime.UtcNow.ToLocalTime() - existingMatch.DeclinedDate.GetValueOrDefault()).TotalDays;

                    if (daysSinceDecline < 7)
                    {
                        var daysRemaining = Math.Ceiling(7 - daysSinceDecline);
                        TempData["Error"] = $"You can send a new request in {daysRemaining} day(s).";
                        await ReloadCreateRequestModel(model);
                        return View(model);
                    }

                    // Update existing declined request to pending with new details
                    existingMatch.Status = "Pending";
                    existingMatch.MatchedDate = DateTime.UtcNow.ToLocalTime(); // Update request date
                    existingMatch.Notes = model.Notes; // Update with new message
                    existingMatch.DeclinedDate = null; // Clear declined date

                    await _context.SaveChangesAsync();

                    // Set success message for re-request
                    TempData["Success"] = $"Your new mentorship request has been sent to {existingMatch.Mentor.FirstName} {existingMatch.Mentor.LastName}!";
                    await ReloadCreateRequestModel(model);
                    ViewBag.RequestSent = true;
                    ViewBag.SentMessage = model.Notes;
                    ViewBag.CanReRequest = false; // Reset flag
                    
                    // Create notification for the mentor about the re-request
                    await CreateMentorshipRequestNotification(model.PartnerId, User.FindFirst("FullName")?.Value);
                    
                    return View(model);
                }
            }

            // Create new request (no existing match found)
            // Get the mentor's PeerMentorship record
            var mentorMentorship = await _context.PeerMentorships
                .FirstOrDefaultAsync(pm => pm.UserId == model.PartnerId);

            // Get or create the mentee's PeerMentorship record
            var menteeMentorship = await _context.PeerMentorships
                .FirstOrDefaultAsync(pm => pm.UserId == userId);

            if (menteeMentorship == null)
            {
                menteeMentorship = new PeerMentorship
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    // Set other required properties based on your PeerMentorship entity
                };
                _context.PeerMentorships.Add(menteeMentorship);
                await _context.SaveChangesAsync();
            }

            // Create the mentorship match with pending status
            var mentorshipMatch = new MentorshipMatch
            {
                Id = Guid.NewGuid(),
                MentorId = model.PartnerId,
                MenteeId = userId,
                MentorMentorshipId = mentorMentorship?.Id ?? Guid.Empty,
                MenteeMentorshipId = menteeMentorship.Id,
                MatchedDate = DateTime.UtcNow.ToLocalTime(),
                Status = "Pending",
                Notes = model.Notes
            };

            _context.MentorshipMatches.Add(mentorshipMatch);
            await _context.SaveChangesAsync();

            // Get mentor name for success message
            var mentor = await _context.UserAccounts
                .FirstOrDefaultAsync(ua => ua.Id == model.PartnerId);

            // Reload the model and show confirmation
            await ReloadCreateRequestModel(model);
            ViewBag.RequestSent = true;
            ViewBag.SentMessage = model.Notes;

            // Create notification for the mentor about the new request
            await CreateMentorshipRequestNotification(model.PartnerId, User.FindFirst("FullName")?.Value);

            return View(model);
        }

        private async Task ReloadCreateRequestModel(CreateMatchRequest model)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                model.Partner = await _context.UserAccounts
                    .Include(ua => ua.UserAccountSkills)
                    .ThenInclude(uas => uas.UserSkill)
                    .FirstOrDefaultAsync(ua => ua.Id == model.PartnerId);

                model.UserSkills = await _context.UserAccountSkills
                    .Where(uas => uas.UserAccountId == userId)
                    .Include(uas => uas.UserSkill)
                    .Select(uas => uas.UserSkill)
                    .ToListAsync();
            }
        }

        private async Task CreateMentorshipRequestNotification(string mentorId, string? menteeName)
        {
            await _notificationService.CreateNotificationAsync(
                mentorId,
                "New Mentorship Request",
                $"You have received a new mentorship request from {menteeName}. Click to review the request.",
                "mentorship_request",
                "<svg fill=\"#000000\" viewBox=\"0 0 16 16\" id=\"request-new-16px\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path id=\"Path_46\" data-name=\"Path 46\" d=\"M-17,11a2,2,0,0,0,2-2,2,2,0,0,0-2-2,2,2,0,0,0-2,2A2,2,0,0,0-17,11Zm0-3a1,1,0,0,1,1,1,1,1,0,0,1-1,1,1,1,0,0,1-1-1A1,1,0,0,1-17,8Zm2.5,4h-5A2.5,2.5,0,0,0-22,14.5,1.5,1.5,0,0,0-20.5,16h7A1.5,1.5,0,0,0-12,14.5,2.5,2.5,0,0,0-14.5,12Zm1,3h-7a.5.5,0,0,1-.5-.5A1.5,1.5,0,0,1-19.5,13h5A1.5,1.5,0,0,1-13,14.5.5.5,0,0,1-13.5,15ZM-6,2.5v5A2.5,2.5,0,0,1-8.5,10h-2.793l-1.853,1.854A.5.5,0,0,1-13.5,12a.489.489,0,0,1-.191-.038A.5.5,0,0,1-14,11.5v-2a.5.5,0,0,1,.5-.5.5.5,0,0,1,.5.5v.793l1.146-1.147A.5.5,0,0,1-11.5,9h3A1.5,1.5,0,0,0-7,7.5v-5A1.5,1.5,0,0,0-8.5,1h-7A1.5,1.5,0,0,0-17,2.5v3a.5.5,0,0,1-.5.5.5.5,0,0,1-.5-.5v-3A2.5,2.5,0,0,1-15.5,0h7A2.5,2.5,0,0,1-6,2.5ZM-11.5,2V4.5H-9a.5.5,0,0,1,.5.5.5.5,0,0,1-.5.5h-2.5V8a.5.5,0,0,1-.5.5.5.5,0,0,1-.5-.5V5.5H-15a.5.5,0,0,1-.5-.5.5.5,0,0,1,.5-.5h2.5V2a.5.5,0,0,1,.5-.5A.5.5,0,0,1-11.5,2Z\" transform=\"translate(22)\"></path> </g></svg>",
                $"/MentorshipMatching/PendingRequests"
            );
        }
        public async Task<IActionResult> MenteeDashboard()
        {
            var currentUserId = GetCurrentUserId();

            var userAccount = await _context.UserAccounts
                .FirstOrDefaultAsync(ua => ua.Id == currentUserId);

            if (userAccount == null)
                return Unauthorized();

            var mentorship = await _context.PeerMentorships
                .FirstOrDefaultAsync(pm => pm.UserId == currentUserId);

            if (mentorship == null)
            {
                TempData["ErrorMessage"] = "You are not registered in the mentorship program.";
                return RedirectToAction("Registration", "PeerMentorship");
            }

            if (mentorship.Role.ToLower() != "mentee")
            {
                return RedirectToAction("MentorDashboard", "MentorshipMatching", new { Id = mentorship.Id });
            }

            var dashboardModel = new MenteeDashboard();

            // Get mentorship requests sent by this mentee
            dashboardModel.RequestsSent = await _context.MentorshipMatches
                .Where(mm => mm.MenteeId == currentUserId)
                .Include(mm => mm.Mentor)
                .Select(mm => new MentorshipRequestViewModel
                {
                    Id = mm.Id,
                    MentorName = $"{mm.Mentor.FirstName} {mm.Mentor.LastName}",
                    MentorPhoto = mm.Mentor.Photo,
                    Status = mm.Status,
                    RequestDate = mm.MatchedDate,
                    StartDate = mm.StartDate,
                    EndDate = mm.EndDate,
                    Notes = mm.Notes
                })
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            // Sessions for mentee (local time semantics): fetch all, then split into proposed and upcoming
            var menteeSessionsAll = await _context.MentorshipSessions
                .Include(s => s.MentorshipMatch)
                .ThenInclude(m => m.Mentor)
                .Include(s => s.MentorshipMatch)
                .ThenInclude(m => m.Mentee)
                .Where(s => (s.MentorshipMatch.MenteeId == currentUserId || s.MentorshipMatch.MentorId == currentUserId) &&
                           s.MentorshipMatch.Status == "Active") // Only include sessions from active mentorships
                .Select(s => new MentorshipSessionItem
                {
                    SessionId = s.Id,
                    MatchId = s.MentorshipMatchId,
                    PartnerName = s.MentorshipMatch.MentorId == currentUserId
                        ? $"{s.MentorshipMatch.Mentee.FirstName} {s.MentorshipMatch.Mentee.LastName}"
                        : $"{s.MentorshipMatch.Mentor.FirstName} {s.MentorshipMatch.Mentor.LastName}",
                    Title = s.Title,
                    StartUtc = s.ScheduledStartUtc,
                    Status = s.Status,
                    IsCreatedByCurrentUser = s.CreatedByUserId == currentUserId
                })
                .ToListAsync();

            dashboardModel.ProposedSessions = menteeSessionsAll
                .Where(s => s.Status == "Proposed")
                .OrderBy(s => s.StartUtc)
                .ToList();

            dashboardModel.UpcomingSessions = menteeSessionsAll
                .Where(s => s.Status == "Confirmed" && s.StartUtc > DateTime.UtcNow.ToLocalTime())
                .OrderBy(s => s.StartUtc)
                .ToList();

            // Get goal progress for the mentee
            dashboardModel.CompletedGoalsCount = await GetCompletedGoalsCountAsync(currentUserId);

            // Only show the most recent mentor notes (adjust takeCount if you need more/less)
            const int takeCount = 2;
            var recentMentorNotes = await _context.MentorSessionNotes
                .Include(n => n.MentorshipMatch)
                .ThenInclude(mm => mm.Mentor)
                .Where(n => n.MentorshipMatch.MenteeId == currentUserId)
                .OrderByDescending(n => n.SubmittedAt)
                .Select(n => new
                {
                    NoteId = n.Id,
                    MatchId = n.MentorshipMatchId,
                    MentorId = n.MentorshipMatch.MentorId,
                    MentorName = n.MentorshipMatch.Mentor.FirstName + " " + n.MentorshipMatch.Mentor.LastName,
                    NoteText = n.Notes,
                    ProgressRating = n.ProgressRating,
                    IsTaskAssigned = n.IsTaskAssigned,
                    TaskTitle = n.TaskTitle,
                    TaskDescription = n.TaskDescription,
                    SubmittedAt = n.SubmittedAt
                })
                .Take(takeCount)
                .ToListAsync();

            ViewBag.RecentMentorNotes = recentMentorNotes;

            return View(dashboardModel);
        }

        private async Task<int> GetCompletedGoalsCountAsync(string userId)
        {
            // Get all active and completed mentorship matches for the user as mentee
            var mentorshipMatches = await _context.MentorshipMatches
                .Where(mm => mm.MenteeId == userId && (mm.Status == "Active" || mm.Status == "Completed"))
                .Select(mm => mm.Id)
                .ToListAsync();

            if (!mentorshipMatches.Any())
                return 0;

            // Get all goal completions for these matches where both mentor and mentee have completed
            var completedGoals = await _context.MentorshipGoalCompletions
                .Where(mgc => mentorshipMatches.Contains(mgc.MentorshipMatchId))
                .GroupBy(mgc => new { mgc.MentorshipMatchId, mgc.GoalId })
                .Where(g => g.Count() >= 2) // Both mentor and mentee have completed
                .CountAsync();

            return completedGoals;
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }
        public async Task<IActionResult> MentorDashboard()
        {
            var currentUserId = GetUserId();

            var userAccount = await _context.UserAccounts
                .FirstOrDefaultAsync(ua => ua.Id == currentUserId);

            if (userAccount == null)
                return Unauthorized();

            var mentorship = await _context.PeerMentorships
                .FirstOrDefaultAsync(pm => pm.UserId == currentUserId);

            if (mentorship == null)
            {
                TempData["ErrorMessage"] = "You are not registered in the mentorship program.";
                return RedirectToAction("Registration", "PeerMentorship");
            }

            if (mentorship.Role.ToLower() != "mentor")
            {
                return RedirectToAction("MenteeDashboard", "MentorshipMatching", new { Id = mentorship.Id });
            }

            var dashboardModel = new MentorDashboard();

            // Get mentorship requests RECEIVED by this mentor
            dashboardModel.RequestsReceived = await _context.MentorshipMatches
                .Where(mm => mm.MentorId == currentUserId) // Current user is the mentor receiving requests
                .Include(mm => mm.Mentee) // Include mentee who sent the request
                .Select(mm => new MentorshipReceivedViewModel
                {
                    Id = mm.Id,
                    MenteeName = $"{mm.Mentee.FirstName} {mm.Mentee.LastName}", // Show mentee name
                    MenteePhoto = mm.Mentee.Photo,
                    Status = mm.Status,
                    RequestDate = mm.MatchedDate,
                    StartDate = mm.StartDate,
                    EndDate = mm.EndDate,
                    Notes = mm.Notes
                })
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            // Sessions for mentor (local): fetch all, then split
            var mentorSessionsAll = await _context.MentorshipSessions
                .Include(s => s.MentorshipMatch)
                .ThenInclude(m => m.Mentee)
                .Include(s => s.MentorshipMatch)
                .ThenInclude(m => m.Mentor)
                .Where(s => (s.MentorshipMatch.MenteeId == currentUserId || s.MentorshipMatch.MentorId == currentUserId) &&
                           s.MentorshipMatch.Status == "Active") // Only include sessions from active mentorships
                .Select(s => new MentorshipSessionItem
                {
                    SessionId = s.Id,
                    MatchId = s.MentorshipMatchId,
                    PartnerName = s.MentorshipMatch.MentorId == currentUserId
                        ? $"{s.MentorshipMatch.Mentee.FirstName} {s.MentorshipMatch.Mentee.LastName}"
                        : $"{s.MentorshipMatch.Mentor.FirstName} {s.MentorshipMatch.Mentor.LastName}",
                    Title = s.Title,
                    StartUtc = s.ScheduledStartUtc,
                    Status = s.Status,
                    IsCreatedByCurrentUser = s.CreatedByUserId == currentUserId
                })
                .ToListAsync();

            dashboardModel.ProposedSessions = mentorSessionsAll
                .Where(s => s.Status == "Proposed")
                .OrderBy(s => s.StartUtc)
                .ToList();

            dashboardModel.UpcomingSessions = mentorSessionsAll
                .Where(s => s.Status == "Confirmed" && s.StartUtc > DateTime.UtcNow.ToLocalTime())
                .OrderBy(s => s.StartUtc)
                .ToList();

            // Get review statistics
            var reviews = await _context.MentorReviews
                .Where(mr => mr.MentorId == currentUserId)
                .ToListAsync();

            if (reviews.Any())
            {
                dashboardModel.TotalReviews = reviews.Count;
                dashboardModel.AverageRating = Math.Round(reviews.Average(r => r.Rating), 1);
                dashboardModel.FourPlusStarReviews = reviews.Count(r => r.Rating >= 4);
                dashboardModel.WouldRecommendCount = reviews.Count(r => r.WouldRecommend);
            }

            return View(dashboardModel);
        }

        private string GetUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }
        [HttpGet]
        public async Task<IActionResult> PendingRequests()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var pendingRequests = await _context.MentorshipMatches
                .Where(mm => mm.MentorId == userId && mm.Status == "Pending")
                .Include(mm => mm.Mentee)
                .ThenInclude(m => m.UserAccountSkills)
                .ThenInclude(uas => uas.UserSkill)
                .OrderByDescending(mm => mm.MatchedDate)
                .ToListAsync();

            return View(pendingRequests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PendingRequests(Guid matchId, string response)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var mentorshipMatch = await _context.MentorshipMatches
                .Include(mm => mm.Mentor)
                .Include(mm => mm.Mentee)
                .FirstOrDefaultAsync(mm => mm.Id == matchId && mm.MentorId == userId);

            if (mentorshipMatch == null)
                return NotFound();

            if (mentorshipMatch.Status != "Pending")
            {
                TempData["Error"] = "This request has already been responded to.";
                return RedirectToAction("PendingRequests");
            }

            if (response.ToLower() == "accept")
            {
                mentorshipMatch.Status = "Active";
                mentorshipMatch.StartDate = DateTime.UtcNow.ToLocalTime();
                // Clear any previous declined date since this is now accepted
                mentorshipMatch.DeclinedDate = null;

                // Create notification for the mentee about the accepted request
                await _notificationService.CreateNotificationAsync(
                    mentorshipMatch.MenteeId, // Mentee's ID
                    "Mentorship Request Accepted!",
                    $"Great news! {mentorshipMatch.Mentor.FirstName} {mentorshipMatch.Mentor.LastName} has accepted your mentorship request. Your mentorship is now active!",
                    "mentorship_accepted",
                    "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path fill-rule=\"evenodd\" clip-rule=\"evenodd\" d=\"M22 12C22 17.5228 17.5228 22 12 22C6.47715 22 2 17.5228 2 12C2 6.47715 6.47715 2 12 2C17.5228 2 22 6.47715 22 12ZM16.0303 8.96967C16.3232 9.26256 16.3232 9.73744 16.0303 10.0303L11.0303 15.0303C10.7374 15.3232 10.2626 15.3232 9.96967 15.0303L7.96967 13.0303C7.67678 12.7374 7.67678 12.2626 7.96967 11.9697C8.26256 11.6768 8.73744 11.6768 9.03033 11.9697L10.5 13.4393L12.7348 11.2045L14.9697 8.96967C15.2626 8.67678 15.7374 8.67678 16.0303 8.96967Z\" fill=\"#54c445\"></path> </g></svg>",
                    $"/MentorshipMatching/MenteeDashboard"
                );

                TempData["Success"] = $"You've accepted the mentorship request from {mentorshipMatch.Mentee.FirstName} {mentorshipMatch.Mentee.LastName}!";
            }
            else if (response.ToLower() == "decline")
            {
                mentorshipMatch.Status = "Declined";
                mentorshipMatch.DeclinedDate = DateTime.UtcNow.ToLocalTime(); // Set the declined date for re-request functionality

                // Create notification for the mentee about the declined request
                await _notificationService.CreateNotificationAsync(
                    mentorshipMatch.MenteeId, // Mentee's ID
                    "Mentorship Request Declined",
                    $"{mentorshipMatch.Mentor.FirstName} {mentorshipMatch.Mentor.LastName} has declined your mentorship request. You can send a new request to the same mentor after 7 days.",
                    "mentorship_declined",
                    "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path fill-rule=\"evenodd\" clip-rule=\"evenodd\" d=\"M22 12C22 17.5228 17.5228 22 12 22C6.47715 22 2 17.5228 2 12C2 6.47715 6.47715 2 12 2C17.5228 2 22 6.47715 22 12ZM8.96963 8.96965C9.26252 8.67676 9.73739 8.67676 10.0303 8.96965L12 10.9393L13.9696 8.96967C14.2625 8.67678 14.7374 8.67678 15.0303 8.96967C15.3232 9.26256 15.3232 9.73744 15.0303 10.0303L13.0606 12L15.0303 13.9696C15.3232 14.2625 15.3232 14.7374 15.0303 15.0303C14.7374 15.3232 14.2625 15.3232 13.9696 15.0303L12 13.0607L10.0303 15.0303C9.73742 15.3232 9.26254 15.3232 8.96965 15.0303C8.67676 14.7374 8.67676 14.2625 8.96965 13.9697L10.9393 12L8.96963 10.0303C8.67673 9.73742 8.67673 9.26254 8.96963 8.96965Z\" fill=\"#f24a4a\"></path> </g></svg>",
                    $"/MentorshipMatching/MenteeDashboard"
                );

                TempData["Info"] = $"You've declined the mentorship request from {mentorshipMatch.Mentee.FirstName} {mentorshipMatch.Mentee.LastName}.";
            }
            else
            {
                TempData["Error"] = "Invalid response.";
                return RedirectToAction("PendingRequests");
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("PendingRequests");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinishMentorship(Guid matchId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var mentorshipMatch = await _context.MentorshipMatches
                .Include(mm => mm.Mentor)
                .Include(mm => mm.Mentee)
                .FirstOrDefaultAsync(mm => mm.Id == matchId && mm.MenteeId == userId);

            if (mentorshipMatch == null)
            {
                return Json(new { success = false, message = "Mentorship match not found." });
            }

            if (mentorshipMatch.Status != "Active")
            {
                return Json(new { success = false, message = "Only active mentorships can be finished." });
            }

            // Update the mentorship status to Completed and set the end date
            mentorshipMatch.Status = "Completed";
            mentorshipMatch.EndDate = DateTime.UtcNow.ToLocalTime();

            await _context.SaveChangesAsync();

            return Json(new { 
                success = true, 
                message = $"Mentorship with {mentorshipMatch.Mentor.FirstName} {mentorshipMatch.Mentor.LastName} has been completed successfully!" 
            });
        }
    }
}