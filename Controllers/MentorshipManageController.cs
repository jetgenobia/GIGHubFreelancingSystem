using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Freelancing.Data;
using Freelancing.Models.Entities;
using Freelancing.Models;
using Freelancing.Services;
using System.Security.Claims;

namespace Freelancing.Controllers
{
    [Authorize]
    public class MentorshipManageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMentorshipSchedulingService _schedulingService;
        private readonly INotificationService _notificationService;

        public MentorshipManageController(ApplicationDbContext context, IMentorshipSchedulingService schedulingService, INotificationService notificationService)
        {
            _context = context;
            _schedulingService = schedulingService;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> Goals(Guid matchId)
        {
            var userId = GetCurrentUserId();
            var match = await _context.MentorshipMatches
                .Include(mm => mm.Mentor)
                .Include(mm => mm.Mentee)
                .FirstOrDefaultAsync(mm => mm.Id == matchId && (mm.MentorId == userId || mm.MenteeId == userId) && (mm.Status == "Active" || mm.Status == "Completed"));
            
            if (match == null)
            {
                TempData["Error"] = "Access denied or mentorship not found";
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }

            var isCurrentUserMentor = match.MentorId == userId;
            var partnerName = isCurrentUserMentor 
                ? $"{match.Mentee.FirstName} {match.Mentee.LastName}"
                : $"{match.Mentor.FirstName} {match.Mentor.LastName}";

            // Get all active goals ordered by their sequence
            var goals = await _context.Goals
                .Where(g => g.IsActive)
                .OrderBy(g => g.Order)
                .ToListAsync();

            // Get completion status for each goal
            var goalCompletions = await _context.MentorshipGoalCompletions
                .Where(mgc => mgc.MentorshipMatchId == matchId)
                .ToListAsync();

            var goalViewModels = new List<GoalItemViewModel>();
            var completedGoalsCount = 0;

            for (int i = 0; i < goals.Count; i++)
            {
                var goal = goals[i];
                var completions = goalCompletions.Where(mgc => mgc.GoalId == goal.Id).ToList();
                
                var isCompletedByMentor = completions.Any(c => c.CompletedByUserId == match.MentorId);
                var isCompletedByMentee = completions.Any(c => c.CompletedByUserId == match.MenteeId);
                var isFullyCompleted = isCompletedByMentor && isCompletedByMentee;

                if (isFullyCompleted)
                {
                    completedGoalsCount++;
                }

                // Determine if current user can mark this goal as done
                var canMarkAsDone = false;
                if (isCurrentUserMentor)
                {
                    canMarkAsDone = !isCompletedByMentor;
                }
                else
                {
                    canMarkAsDone = !isCompletedByMentee;
                }

                // Determine if the mark as done button should be shown
                // Only show if previous goal is completed (except for the first goal)
                var showMarkAsDoneButton = false;
                if (i == 0) // First goal
                {
                    showMarkAsDoneButton = canMarkAsDone;
                }
                else // Subsequent goals
                {
                    var previousGoal = goals[i - 1];
                    var previousCompletions = goalCompletions.Where(mgc => mgc.GoalId == previousGoal.Id).ToList();
                    var previousCompletedByMentor = previousCompletions.Any(c => c.CompletedByUserId == match.MentorId);
                    var previousCompletedByMentee = previousCompletions.Any(c => c.CompletedByUserId == match.MenteeId);
                    var previousFullyCompleted = previousCompletedByMentor && previousCompletedByMentee;
                    
                    showMarkAsDoneButton = previousFullyCompleted && canMarkAsDone;
                }

                // Determine completion status text
                string completedBy = "";
                if (isFullyCompleted)
                {
                    completedBy = "Both";
                }
                else if (isCompletedByMentor)
                {
                    completedBy = "Mentor";
                }
                else if (isCompletedByMentee)
                {
                    completedBy = "Mentee";
                }

                var goalViewModel = new GoalItemViewModel
                {
                    GoalId = goal.Id,
                    GoalName = goal.GoalName,
                    GoalDescription = goal.GoalDescription,
                    Order = goal.Order,
                    IsCompletedByMentor = isCompletedByMentor,
                    IsCompletedByMentee = isCompletedByMentee,
                    CanMarkAsDone = canMarkAsDone,
                    ShowMarkAsDoneButton = showMarkAsDoneButton,
                    CompletedBy = completedBy,
                    CompletedAt = completions.Any() ? completions.Max(c => c.CompletedAt) : null,
                    IconSvg = goal.IconSvg
                };

                goalViewModels.Add(goalViewModel);
            }

            var viewModel = new GoalViewModel
            {
                MatchId = matchId,
                PartnerName = partnerName,
                IsCurrentUserMentor = isCurrentUserMentor,
                Goals = goalViewModels,
                TotalGoals = goals.Count,
                CompletedGoals = completedGoalsCount
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Sessions(Guid matchId)
        {
            var userId = GetCurrentUserId();
            var match = await _context.MentorshipMatches
                .FirstOrDefaultAsync(mm => mm.Id == matchId && (mm.MentorId == userId || mm.MenteeId == userId) && (mm.Status == "Active" || mm.Status == "Completed"));
            if (match == null)
            {
                TempData["Error"] = "Access denied or mentorship not found";
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }

            var sessions = await _schedulingService.GetSessionsAsync(matchId);
            ViewBag.MatchId = matchId;
            ViewBag.CurrentUserId = userId;
            return View(sessions);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSession(Guid matchId, DateTime startUtc, string? title, string? notes, string? timeZone = null, int tzOffsetMinutes = 0)
        {
            var userId = GetCurrentUserId();
            var match = await _context.MentorshipMatches
                .Include(mm => mm.Mentor)
                .Include(mm => mm.Mentee)
                .FirstOrDefaultAsync(mm => mm.Id == matchId && (mm.MentorId == userId || mm.MenteeId == userId) && (mm.Status == "Active" || mm.Status == "Completed"));
            if (match == null)
            {
                TempData["Error"] = "Access denied or mentorship not found";
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }

            // Per requirement: treat inputs as local and store/display consistently (no UTC conversion)
            var result = await _schedulingService.CreateSessionAsync(matchId, userId, startUtc, title, notes, timeZone);
            if (!result.ok)
            {
                TempData["Error"] = result.error;
            }
            else
            {
                TempData["Success"] = "Session proposed";
                
                // Send notification to the target mentor/mentee
                var isCurrentUserMentor = match.MentorId == userId;
                var targetUserId = isCurrentUserMentor ? match.MenteeId : match.MentorId;
                var requestorName = isCurrentUserMentor 
                    ? $"{match.Mentor.FirstName} {match.Mentor.LastName}"
                    : $"{match.Mentee.FirstName} {match.Mentee.LastName}";
                
                // Determine the target user's role and appropriate redirect URL
                var isTargetUserMentor = match.MentorId == targetUserId;
                var redirectUrl = isTargetUserMentor 
                    ? $"/MentorshipMatching/MentorDashboard?matchId={matchId}"
                    : $"/MentorshipMatching/MenteeDashboard?matchId={matchId}";
                
                var calendarIconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path d=\"M3 9H21M12 18V12M15 15.001L9 15M7 3V5M17 3V5M6.2 21H17.8C18.9201 21 19.4802 21 19.908 20.782C20.2843 20.5903 20.5903 20.2843 20.782 19.908C21 19.4802 21 18.9201 21 17.8V8.2C21 7.07989 21 6.51984 20.782 6.09202C20.5903 5.71569 20.2843 5.40973 19.908 5.21799C19.4802 5 18.9201 5 17.8 5H6.2C5.0799 5 4.51984 5 4.09202 5.21799C3.71569 5.40973 3.40973 5.71569 3.21799 6.09202C3 6.51984 3 7.07989 3 8.2V17.8C3 18.9201 3 19.4802 3.21799 19.908C3.40973 20.2843 3.71569 20.5903 4.09202 20.782C4.51984 21 5.07989 21 6.2 21Z\" stroke=\"#000000\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></path> </g></svg>";
                
                var sessionDate = startUtc.ToString("MMMM dd, yyyy 'at' h:mm tt");
                var notificationTitle = "New Session Proposal";
                var notificationMessage = $"{requestorName} has proposed a new session for {sessionDate}";
                if (!string.IsNullOrEmpty(title))
                {
                    notificationMessage += $": {title}";
                }
                
                await _notificationService.CreateNotificationAsync(
                    targetUserId,
                    notificationTitle,
                    notificationMessage,
                    "session_proposal",
                    calendarIconSvg,
                    redirectUrl
                );
            }
            return RedirectToAction("Sessions", new { matchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptSession(Guid sessionId, string? returnUrl)
        {
            var userId = GetCurrentUserId();
            var session = await _schedulingService.GetSessionAsync(sessionId);
            if (session == null)
            {
                TempData["Error"] = "Session not found";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }

            var result = await _schedulingService.AcceptAsync(sessionId, userId);
            if (result.ok)
            {
                TempData["Success"] = "Session confirmed";
                
                // Send notification to the session creator
                var match = await _context.MentorshipMatches
                    .Include(mm => mm.Mentor)
                    .Include(mm => mm.Mentee)
                    .FirstOrDefaultAsync(mm => mm.Id == session.MentorshipMatchId);
                
                if (match != null)
                {
                    var responderName = match.MentorId == userId 
                        ? $"{match.Mentor.FirstName} {match.Mentor.LastName}"
                        : $"{match.Mentee.FirstName} {match.Mentee.LastName}";
                    
                    var sessionDate = session.ScheduledStartUtc.ToString("MMMM dd, yyyy 'at' h:mm tt");
                    var notificationTitle = "Session Accepted";
                    var notificationMessage = $"{responderName} has accepted your session proposal for {sessionDate}";
                    if (!string.IsNullOrEmpty(session.Title))
                    {
                        notificationMessage += $": {session.Title}";
                    }
                    
                    var acceptedIconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path d=\"M3 9H21M9 15L11 17L15 13M7 3V5M17 3V5M6.2 21H17.8C18.9201 21 19.4802 21 19.908 20.782C20.2843 20.5903 20.5903 20.2843 20.782 19.908C21 19.4802 21 18.9201 21 17.8V8.2C21 7.07989 21 6.51984 20.782 6.09202C20.5903 5.71569 20.2843 5.40973 19.908 5.21799C19.4802 5 18.9201 5 17.8 5H6.2C5.0799 5 4.51984 5 4.09202 5.21799C3.71569 5.40973 3.40973 5.71569 3.21799 6.09202C3 6.51984 3 7.07989 3 8.2V17.8C3 18.9201 3 19.4802 3.21799 19.908C3.40973 20.2843 3.71569 20.5903 4.09202 20.782C4.51984 21 5.07989 21 6.2 21Z\" stroke=\"#000000\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></path> </g></svg>";
                    
                    // Determine the target user's role and appropriate redirect URL
                    var isTargetUserMentor = match.MentorId == session.CreatedByUserId;
                    var redirectUrl = isTargetUserMentor 
                        ? $"/MentorshipMatching/MentorDashboard?matchId={session.MentorshipMatchId}"
                        : $"/MentorshipMatching/MenteeDashboard?matchId={session.MentorshipMatchId}";
                    
                    await _notificationService.CreateNotificationAsync(
                        session.CreatedByUserId,
                        notificationTitle,
                        notificationMessage,
                        "session_accepted",
                        acceptedIconSvg,
                        redirectUrl
                    );
                }
            }
            else
            {
                TempData["Error"] = result.error;
            }
            
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Sessions", new { matchId = session.MentorshipMatchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineSession(Guid sessionId, string? returnUrl)
        {
            var userId = GetCurrentUserId();
            var session = await _schedulingService.GetSessionAsync(sessionId);
            if (session == null)
            {
                TempData["Error"] = "Session not found";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }
            var result = await _schedulingService.DeclineAsync(sessionId, userId);
            if (result.ok)
            {
                TempData["Success"] = "Session declined";
                
                // Send notification to the session creator
                var match = await _context.MentorshipMatches
                    .Include(mm => mm.Mentor)
                    .Include(mm => mm.Mentee)
                    .FirstOrDefaultAsync(mm => mm.Id == session.MentorshipMatchId);
                
                if (match != null)
                {
                    var responderName = match.MentorId == userId 
                        ? $"{match.Mentor.FirstName} {match.Mentor.LastName}"
                        : $"{match.Mentee.FirstName} {match.Mentee.LastName}";
                    
                    var sessionDate = session.ScheduledStartUtc.ToString("MMMM dd, yyyy 'at' h:mm tt");
                    var notificationTitle = "Session Declined";
                    var notificationMessage = $"{responderName} has declined your session proposal for {sessionDate}";
                    if (!string.IsNullOrEmpty(session.Title))
                    {
                        notificationMessage += $": {session.Title}";
                    }
                    
                    var declinedIconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path d=\"M10 13L14 17M14 13L10 17M3 9H21M7 3V5M17 3V5M6.2 21H17.8C18.9201 21 19.4802 21 19.908 20.782C20.2843 20.5903 20.5903 20.2843 20.782 19.908C21 19.4802 21 18.9201 21 17.8V8.2C21 7.07989 21 6.51984 20.782 6.09202C20.5903 5.71569 20.2843 5.40973 19.908 5.21799C19.4802 5 18.9201 5 17.8 5H6.2C5.0799 5 4.51984 5 4.09202 5.21799C3.71569 5.40973 3.40973 5.71569 3.21799 6.09202C3 6.51984 3 7.07989 3 8.2V17.8C3 18.9201 3 19.4802 3.21799 19.908C3.40973 20.2843 3.71569 20.5903 4.09202 20.782C4.51984 21 5.07989 21 6.2 21Z\" stroke=\"#000000\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></path> </g></svg>";
                    
                    // Determine the target user's role and appropriate redirect URL
                    var isTargetUserMentor = match.MentorId == session.CreatedByUserId;
                    var redirectUrl = isTargetUserMentor 
                        ? $"/MentorshipMatching/MentorDashboard?matchId={session.MentorshipMatchId}"
                        : $"/MentorshipMatching/MenteeDashboard?matchId={session.MentorshipMatchId}";
                    
                    await _notificationService.CreateNotificationAsync(
                        session.CreatedByUserId,
                        notificationTitle,
                        notificationMessage,
                        "session_declined",
                        declinedIconSvg,
                        redirectUrl
                    );
                }
            }
            else
            {
                TempData["Error"] = result.error;
            }
            
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Sessions", new { matchId = session.MentorshipMatchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSession(Guid sessionId)
        {
            var userId = GetCurrentUserId();
            var session = await _schedulingService.GetSessionAsync(sessionId);
            if (session == null)
            {
                TempData["Error"] = "Session not found";
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }
            var result = await _schedulingService.CancelAsync(sessionId, userId);
            TempData[result.ok ? "Success" : "Error"] = result.ok ? "Session cancelled" : result.error;
            return RedirectToAction("Sessions", new { matchId = session.MentorshipMatchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleSession(Guid sessionId, DateTime startUtc, string? notes)
        {
            var userId = GetCurrentUserId();
            var session = await _schedulingService.GetSessionAsync(sessionId);
            if (session == null)
            {
                TempData["Error"] = "Session not found";
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }
            var result = await _schedulingService.RescheduleAsync(sessionId, userId, startUtc, notes);
            TempData[result.ok ? "Success" : "Error"] = result.ok ? "Session rescheduled" : result.error;
            return RedirectToAction("Sessions", new { matchId = session.MentorshipMatchId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkGoalAsDone(Guid matchId, Guid goalId)
        {
            var userId = GetCurrentUserId();
            var match = await _context.MentorshipMatches
                .FirstOrDefaultAsync(mm => mm.Id == matchId && (mm.MentorId == userId || mm.MenteeId == userId) && (mm.Status == "Active" || mm.Status == "Completed"));
            
            if (match == null)
            {
                TempData["Error"] = "Access denied or mentorship not found";
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }

            var isCurrentUserMentor = match.MentorId == userId;
            var completionType = isCurrentUserMentor ? "Mentor" : "Mentee";

            // Check if goal already completed by this user
            var existingCompletion = await _context.MentorshipGoalCompletions
                .FirstOrDefaultAsync(mgc => mgc.MentorshipMatchId == matchId && 
                                          mgc.GoalId == goalId && 
                                          mgc.CompletedByUserId == userId);

            if (existingCompletion != null)
            {
                TempData["Error"] = "You have already marked this goal as done";
                return RedirectToAction("Goals", new { matchId });
            }

            // Verify that the goal exists and is active
            var goal = await _context.Goals
                .FirstOrDefaultAsync(g => g.Id == goalId && g.IsActive);

            if (goal == null)
            {
                TempData["Error"] = "Goal not found";
                return RedirectToAction("Goals", new { matchId });
            }

            // Check if previous goal is completed (except for the first goal)
            if (goal.Order > 1)
            {
                var previousGoal = await _context.Goals
                    .FirstOrDefaultAsync(g => g.Order == goal.Order - 1 && g.IsActive);

                if (previousGoal != null)
                {
                    var previousCompletions = await _context.MentorshipGoalCompletions
                        .Where(mgc => mgc.MentorshipMatchId == matchId && mgc.GoalId == previousGoal.Id)
                        .ToListAsync();

                    var previousCompletedByMentor = previousCompletions.Any(c => c.CompletedByUserId == match.MentorId);
                    var previousCompletedByMentee = previousCompletions.Any(c => c.CompletedByUserId == match.MenteeId);
                    var previousFullyCompleted = previousCompletedByMentor && previousCompletedByMentee;

                    if (!previousFullyCompleted)
                    {
                        TempData["Error"] = "Previous goal must be completed before marking this goal as done";
                        return RedirectToAction("Goals", new { matchId });
                    }
                }
            }

            // Create the completion record
            var completion = new MentorshipGoalCompletion
            {
                MentorshipMatchId = matchId,
                GoalId = goalId,
                CompletedByUserId = userId,
                CompletedAt = DateTime.UtcNow.ToLocalTime(),
                CompletionType = completionType
            };

            _context.MentorshipGoalCompletions.Add(completion);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Goal marked as done successfully";
            return RedirectToAction("Goals", new { matchId });
        }

        [HttpGet]
        public async Task<IActionResult> Feedback(Guid matchId)
        {
            var userId = GetCurrentUserId();
            var match = await _context.MentorshipMatches
                .Include(mm => mm.Mentor)
                .Include(mm => mm.Mentee)
                .FirstOrDefaultAsync(mm => mm.Id == matchId && mm.MenteeId == userId && mm.Status == "Completed");
            
            if (match == null)
            {
                TempData["Error"] = "Access denied or completed mentorship not found";
                return RedirectToAction("MenteeDashboard", "MentorshipMatching");
            }

            // Check if review already exists
            var existingReview = await _context.MentorReviews
                .FirstOrDefaultAsync(mr => mr.MentorshipMatchId == matchId);

            if (existingReview != null)
            {
                TempData["Error"] = "You have already submitted a review for this mentorship";
                return RedirectToAction("MenteeDashboard", "MentorshipMatching");
            }

            var viewModel = new MentorFeedbackViewModel
            {
                MatchId = matchId,
                MentorName = $"{match.Mentor.FirstName} {match.Mentor.LastName}",
                MentorPhoto = match.Mentor.Photo,
                MenteeName = $"{match.Mentee.FirstName} {match.Mentee.LastName}",
                MatchStartDate = match.StartDate ?? match.MatchedDate,
                MatchEndDate = match.EndDate
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Feedback(MentorFeedbackViewModel model)
        {
            var userId = GetCurrentUserId();
            var match = await _context.MentorshipMatches
                .Include(mm => mm.Mentor)
                .Include(mm => mm.Mentee)
                .FirstOrDefaultAsync(mm => mm.Id == model.MatchId && mm.MenteeId == userId && mm.Status == "Completed");
            
            if (match == null)
            {
                TempData["Error"] = "Access denied or completed mentorship not found";
                return RedirectToAction("MenteeDashboard", "MentorshipMatching");
            }

            // Check if review already exists
            var existingReview = await _context.MentorReviews
                .FirstOrDefaultAsync(mr => mr.MentorshipMatchId == model.MatchId);

            if (existingReview != null)
            {
                TempData["Error"] = "You have already submitted a review for this mentorship";
                return RedirectToAction("MenteeDashboard", "MentorshipMatching");
            }

            if (!ModelState.IsValid)
            {
                // Repopulate the view model with mentor info
                model.MentorName = $"{match.Mentor.FirstName} {match.Mentor.LastName}";
                model.MentorPhoto = match.Mentor.Photo;
                model.MenteeName = $"{match.Mentee.FirstName} {match.Mentee.LastName}";
                model.MatchStartDate = match.StartDate ?? match.MatchedDate;
                model.MatchEndDate = match.EndDate;
                return View(model);
            }

            var review = new MentorReview
            {
                MentorshipMatchId = model.MatchId,
                MentorId = match.MentorId,
                MenteeId = userId,
                Rating = model.Rating,
                WouldRecommend = model.WouldRecommend,
                Comments = model.Comments,
                Strengths = model.Strengths,
                AreasForImprovement = model.AreasForImprovement,

                CreatedAt = DateTime.UtcNow.ToLocalTime()
            };

            _context.MentorReviews.Add(review);
            await _context.SaveChangesAsync();

            // Send notification to the mentor about the review
            await _notificationService.CreateNotificationAsync(
                match.MentorId,
                "New Review Received",
                $"{match.Mentee.FirstName} {match.Mentee.LastName} has submitted a review for your mentorship.",
                "mentor_review",
                "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path d=\"M16 1C17.6569 1 19 2.34315 19 4C19 4.55228 18.5523 5 18 5C17.4477 5 17 4.55228 17 4C17 3.44772 16.5523 3 16 3H4C3.44772 3 3 3.44772 3 4V20C3 20.5523 3.44772 21 4 21H16C16.5523 21 17 20.5523 17 20V19C17 18.4477 17.4477 18 18 18C18.5523 18 19 18.4477 19 19V20C19 21.6569 17.6569 23 16 23H4C2.34315 23 1 21.6569 1 20V4C1 2.34315 2.34315 1 4 1H16Z\" fill=\"#0F0F0F\"></path> <path fill-rule=\"evenodd\" clip-rule=\"evenodd\" d=\"M20.7991 8.20087C20.4993 7.90104 20.0132 7.90104 19.7133 8.20087L11.9166 15.9977C11.7692 16.145 11.6715 16.3348 11.6373 16.5404L11.4728 17.5272L12.4596 17.3627C12.6652 17.3285 12.855 17.2308 13.0023 17.0835L20.7991 9.28666C21.099 8.98682 21.099 8.5007 20.7991 8.20087ZM18.2991 6.78666C19.38 5.70578 21.1325 5.70577 22.2134 6.78665C23.2942 7.86754 23.2942 9.61999 22.2134 10.7009L14.4166 18.4977C13.9744 18.9398 13.4052 19.2327 12.7884 19.3355L11.8016 19.5C10.448 19.7256 9.2744 18.5521 9.50001 17.1984L9.66448 16.2116C9.76728 15.5948 10.0602 15.0256 10.5023 14.5834L18.2991 6.78666Z\" fill=\"#0F0F0F\"></path> <path d=\"M5 7C5 6.44772 5.44772 6 6 6H14C14.5523 6 15 6.44772 15 7C15 7.55228 14.5523 8 14 8H6C5.44772 8 5 7.55228 5 7Z\" fill=\"#0F0F0F\"></path> <path d=\"M5 11C5 10.4477 5.44772 10 6 10H10C10.5523 10 11 10.4477 11 11C11 11.5523 10.5523 12 10 12H6C5.44772 12 5 11.5523 5 11Z\" fill=\"#0F0F0F\"></path> <path d=\"M5 15C5 14.4477 5.44772 14 6 14H7C7.55228 14 8 14.4477 8 15C8 15.5523 7.55228 16 7 16H6C5.44772 16 5 15.5523 5 15Z\" fill=\"#0F0F0F\"></path> </g></svg>",
                "/MentorshipManage/MyReviews"
            );

            TempData["Success"] = "Thank you for your feedback! Your review has been submitted successfully.";
            return RedirectToAction("MenteeDashboard", "MentorshipMatching");
        }

        [HttpGet]
        public async Task<IActionResult> MyReviews()
        {
            var userId = GetCurrentUserId();
            
            var reviews = await _context.MentorReviews
                .Include(mr => mr.Mentee)
                .Include(mr => mr.MentorshipMatch)
                .Where(mr => mr.MentorId == userId)
                .OrderByDescending(mr => mr.CreatedAt)
                .Select(mr => new MentorReviewDisplayViewModel
                {
                    Id = mr.Id,
                    MenteeName = $"{mr.Mentee.FirstName} {mr.Mentee.LastName}",
                    MenteePhoto = mr.Mentee.Photo,
                    Rating = mr.Rating,
                    WouldRecommend = mr.WouldRecommend,
                    Comments = mr.Comments,
                    Strengths = mr.Strengths,
                    AreasForImprovement = mr.AreasForImprovement,
                    CreatedAt = mr.CreatedAt,

                    MentorName = $"{mr.Mentor.FirstName} {mr.Mentor.LastName}",
                    MatchStartDate = mr.MentorshipMatch.StartDate ?? mr.MentorshipMatch.MatchedDate,
                    MatchEndDate = mr.MentorshipMatch.EndDate
                })
                .ToListAsync();

            return View(reviews);
        }

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }
    }
}
