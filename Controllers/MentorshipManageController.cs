using Freelancing.Data;
using Freelancing.Models;
using Freelancing.Models.Entities;
using Freelancing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Freelancing.Controllers
{
    [Authorize]
    public class MentorshipManageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMentorshipSchedulingService _schedulingService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<MentorshipManageController> _logger;

        public MentorshipManageController(ApplicationDbContext context, IMentorshipSchedulingService schedulingService, INotificationService notificationService, ILogger<MentorshipManageController> logger)
        {
            _context = context;
            _schedulingService = schedulingService;
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Goals(Guid matchId)
        {
            var userId = GetCurrentUserId();
            var mentorshipMatch = await _context.MentorshipMatches
                .Include(mm => mm.Mentor)
                .Include(mm => mm.Mentee)
                .FirstOrDefaultAsync(mm => mm.Id == matchId && (mm.MentorId == userId || mm.MenteeId == userId) && (mm.Status == "Active" || mm.Status == "Completed"));

            if (mentorshipMatch == null)
            {
                TempData["Error"] = "Access denied or mentorship not found";
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }

            var isCurrentUserMentor = mentorshipMatch.MentorId == userId;
            var partnerName = isCurrentUserMentor
                ? $"{mentorshipMatch.Mentee.FirstName} {mentorshipMatch.Mentee.LastName}"
                : $"{mentorshipMatch.Mentor.FirstName} {mentorshipMatch.Mentor.LastName}";

            // Get all active goals ordered by their sequence
            var goals = await _context.Goals
                .Where(g => g.IsActive)
                .OrderBy(g => g.Order)
                .ToListAsync();

            // Get completion status for each goal
            var goalCompletions = await _context.MentorshipGoalCompletions
                .Where(mgc => mgc.MentorshipMatchId == matchId)
                .ToListAsync();

            // Get evidence and notes for checking submission status
            var evidenceSubmissions = await _context.MenteeSessionEvidences
                .Where(mse => mse.MentorshipMatchId == matchId)
                .ToListAsync();

            var mentorNotes = await _context.MentorSessionNotes
                .Where(msn => msn.MentorshipMatchId == matchId)
                .ToListAsync();

            var goalViewModels = new List<GoalItemViewModel>();
            var completedGoalsCount = 0;

            for (int i = 0; i < goals.Count; i++)
            {
                var goal = goals[i];
                var completions = goalCompletions.Where(mgc => mgc.GoalId == goal.Id).ToList();

                var isCompletedByMentor = completions.Any(c => c.CompletedByUserId == mentorshipMatch.MentorId);
                var isCompletedByMentee = completions.Any(c => c.CompletedByUserId == mentorshipMatch.MenteeId);
                var isFullyCompleted = isCompletedByMentor && isCompletedByMentee;

                if (isFullyCompleted)
                {
                    completedGoalsCount++;
                }

                // Check if evidence and notes have been submitted
                var hasEvidence = evidenceSubmissions.Any(es => es.GoalId == goal.Id && es.UserId == mentorshipMatch.MenteeId);
                var hasMentorNote = mentorNotes.Any(mn => mn.GoalId == goal.Id && mn.MentorId == mentorshipMatch.MentorId);

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
                    var previousCompletedByMentor = previousCompletions.Any(c => c.CompletedByUserId == mentorshipMatch.MentorId);
                    var previousCompletedByMentee = previousCompletions.Any(c => c.CompletedByUserId == mentorshipMatch.MenteeId);
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
                    IconSvg = goal.IconSvg,
                    IsFullyCompleted = isFullyCompleted,

                    // Evidence and note tracking
                    HasMenteeEvidence = hasEvidence,
                    HasMentorNote = hasMentorNote,
                    IsCurrentUserMentor = isCurrentUserMentor,

                    // Custom goal properties
                    IsCustomGoal = goal.IsCustom,
                    Priority = goal.Priority,
                    TargetDate = goal.TargetDate,
                    IsOverdue = goal.TargetDate.HasValue && goal.TargetDate.Value < DateTime.UtcNow && !isFullyCompleted,
                    Category = goal.Category,
                    SuccessCriteria = !string.IsNullOrEmpty(goal.SuccessCriteria) ?
                     goal.SuccessCriteria.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList() :
                     new List<string>(),
                    CanDelete = goal.IsCustom && goal.CreatedBy == userId && isCurrentUserMentor
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
        public async Task<IActionResult> CreateGoal(Guid matchId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var match = await _context.MentorshipMatches
                .FirstOrDefaultAsync(m => m.Id == matchId &&
                    (m.MentorId == userId || m.MenteeId == userId));

            if (match == null) return NotFound();

            // Check if current user is a mentor
            var isCurrentUserMentor = match.MentorId == userId;
            if (!isCurrentUserMentor)
            {
                TempData["Error"] = "Only mentors can create custom goals.";
                return RedirectToAction("Goals", new { matchId });
            }

            var model = new CreateGoalViewModel
            {
                MatchId = matchId
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGoal(CreateGoalViewModel model)
        {
            var userId = GetCurrentUserId();

            // Validate that user has permission to create goals for this match
            var match = await _context.MentorshipMatches
                .FirstOrDefaultAsync(m => m.Id == model.MatchId &&
                    (m.MentorId == userId || m.MenteeId == userId));

            if (match == null)
            {
                TempData["Error"] = "Access denied or mentorship not found.";
                return RedirectToAction("Goals", new { matchId = model.MatchId });
            }

            // Check if current user is a mentor
            var isCurrentUserMentor = match.MentorId == userId;
            if (!isCurrentUserMentor)
            {
                TempData["Error"] = "Only mentors can create custom goals.";
                return RedirectToAction("Goals", new { matchId = model.MatchId });
            }

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                // Create custom goal
                var customGoal = new Goal
                {
                    Id = Guid.NewGuid(),
                    GoalName = model.GoalName,
                    GoalDescription = model.GoalDescription,
                    IsActive = true,
                    IconSvg = "<svg class= \"\"w-[85px] h-[85px]\" version=\"1.0\" id=\"Layer_1\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 64 64\" enable-background=\"new 0 0 64 64\" xml:space=\"preserve\" fill=\"#000000\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <g> <path fill=\"#394240\" d=\"M63.414,23.414c0.781-0.781,0.781-2.047,0-2.828l-20-20C43.023,0.195,42.512,0,42,0 s-1.023,0.195-1.414,0.586l-12,12c-1.381,1.381-3.547,2.081-6.438,2.081c-5.429,0-11.304-2.48-11.362-2.506 C10.534,12.054,10.267,12,10,12c-0.332,0-0.662,0.082-0.96,0.246c-0.538,0.295-0.912,0.82-1.013,1.425l-8,48 c-0.106,0.638,0.102,1.286,0.559,1.743C0.964,63.792,1.474,64,2,64c0.109,0,0.219-0.009,0.329-0.027l48-8 c0.605-0.101,1.131-0.475,1.426-1.013c0.295-0.539,0.325-1.184,0.083-1.748c-1.521-3.548-4.561-13.661-0.424-17.798L63.414,23.414z M50.707,33.293l-20-20l0,0l1.586-1.586l0,0l20,20L50.707,33.293z M41.998,2C41.999,2,41.999,2,41.998,2L62,22l-8.293,8.293l-20-20 l0,0L41.998,2z M47.52,44.78c0.442,3.563,1.571,7.099,2.48,9.22L3.698,61.717l20.549-20.55C25.038,41.691,25.982,42,27,42 c2.757,0,5-2.243,5-5s-2.243-5-5-5s-5,2.243-5,5c0,1.018,0.309,1.963,0.833,2.753L2.282,60.305l7.709-46.309 c0.062,0.027,6.233,2.671,12.157,2.671c2.988,0,5.372-0.679,7.107-2.017c0.015,0.018,0.021,0.04,0.037,0.057l20,20 c0.02,0.02,0.045,0.025,0.064,0.043C47.599,37.024,46.975,40.386,47.52,44.78z M24,37c0-1.654,1.346-3,3-3s3,1.346,3,3 s-1.346,3-3,3S24,38.654,24,37z\"></path> <polygon fill=\"#506C7F\" points=\"50.707,33.293 30.707,13.293 30.707,13.293 32.293,11.707 32.293,11.707 52.293,31.707 \"></polygon> <polygon fill=\"#F76D57\" points=\"41.998,2 41.999,2 62,22 53.707,30.293 33.707,10.293 33.707,10.293 \"></polygon> <path fill=\"#F9EBB2\" d=\"M47.52,44.78c0.442,3.563,1.571,7.099,2.48,9.22L3.698,61.717l20.549-20.55C25.038,41.691,25.982,42,27,42 c2.757,0,5-2.243,5-5s-2.243-5-5-5s-5,2.243-5,5c0,1.018,0.309,1.963,0.833,2.753L2.282,60.305l7.709-46.309 c0.062,0.027,6.233,2.671,12.157,2.671c2.988,0,5.372-0.679,7.107-2.017c0.015,0.018,0.021,0.04,0.037,0.057l20,20 c0.02,0.02,0.045,0.025,0.064,0.043C47.599,37.024,46.975,40.386,47.52,44.78z\"></path> <circle fill=\"#B4CCB9\" cx=\"27\" cy=\"37\" r=\"3\"></circle> </g> </g></svg>",
                    Order = await GetNextGoalOrderAsync(),

                    // Custom goal properties
                    IsCustom = true,
                    Priority = model.Priority ?? "Medium",
                    TargetDate = model.TargetDate,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    Category = model.Category,
                    SuccessCriteria = model.SuccessCriteria?.Any() == true ?
                                     string.Join("|", model.SuccessCriteria.Where(s => !string.IsNullOrWhiteSpace(s))) : null
                };

                _context.Goals.Add(customGoal);
                await _context.SaveChangesAsync();

                // Notify mentee about new custom goal
                var menteeId = match.MenteeId;
                var mentorName = User.FindFirst("FullName")?.Value ?? "Your mentor";

                TempData["Success"] = "Custom goal created successfully!";
                return RedirectToAction("Goals", new { matchId = model.MatchId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while creating the goal. Please try again.";
                // Log the exception here if you have logging configured
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGoal(Guid goalId, Guid matchId)
        {
            var userId = GetCurrentUserId();

            // First check if the user is a mentor in this match
            var match = await _context.MentorshipMatches
                .FirstOrDefaultAsync(m => m.Id == matchId &&
                    (m.MentorId == userId || m.MenteeId == userId));

            if (match == null)
            {
                TempData["Error"] = "Access denied or mentorship not found.";
                return RedirectToAction("Goals", new { matchId });
            }

            var isCurrentUserMentor = match.MentorId == userId;
            if (!isCurrentUserMentor)
            {
                TempData["Error"] = "Only mentors can delete custom goals.";
                return RedirectToAction("Goals", new { matchId });
            }

            var goal = await _context.Goals
                .FirstOrDefaultAsync(g => g.Id == goalId && g.CreatedBy == userId);

            if (goal == null)
            {
                TempData["Error"] = "Goal not found or you don't have permission to delete it.";
                return RedirectToAction("Goals", new { matchId });
            }

            // Check if goal has any completions
            var hasCompletions = await _context.MentorshipGoalCompletions
                .AnyAsync(mgc => mgc.GoalId == goalId);

            if (hasCompletions)
            {
                // Soft delete to preserve completion history
                goal.IsActive = false;
                goal.DeletedAt = DateTime.UtcNow;
            }
            else
            {
                // Hard delete if no completions exist
                _context.Goals.Remove(goal);
            }

            try
            {
                await _context.SaveChangesAsync();

                // Notify mentee about deleted goal
                var menteeId = match.MenteeId;
                var mentorName = User.FindFirst("FullName")?.Value ?? "Your mentor";

                TempData["Success"] = "Goal deleted successfully!";
            }
            catch (Exception)
            {
                TempData["Error"] = "An error occurred while deleting the goal. Please try again.";
            }

            return RedirectToAction("Goals", new { matchId });
        }

        private async Task<int> GetNextGoalOrderAsync()
        {
            var maxOrder = await _context.Goals
                .Where(g => g.IsActive)
                .Select(g => (int?)g.Order)
                .MaxAsync();

            return (maxOrder ?? 0) - 1;
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

        [HttpGet]
        public async Task<IActionResult> SubmitMenteeEvidence(Guid matchId, Guid goalId)
        {
            var userId = GetCurrentUserId();
            var match = await _context.MentorshipMatches
                .Include(mm => mm.Mentee)
                .FirstOrDefaultAsync(mm => mm.Id == matchId && mm.MenteeId == userId && (mm.Status == "Active" || mm.Status == "Completed"));

            if (match == null)
            {
                TempData["Error"] = "Access denied or mentorship not found";
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }

            var goal = await _context.Goals
                .FirstOrDefaultAsync(g => g.Id == goalId && g.IsActive);

            if (goal == null)
            {
                TempData["Error"] = "Goal not found";
                return RedirectToAction("Goals", new { matchId });
            }

            // Check if evidence already submitted
            var existingEvidence = await _context.MenteeSessionEvidences
                .FirstOrDefaultAsync(mse => mse.MentorshipMatchId == matchId &&
                                           mse.GoalId == goalId &&
                                           mse.UserId == userId);

            var mentorNote = await _context.MentorSessionNotes
                .Where(msn => msn.MentorshipMatchId == matchId && msn.GoalId == goalId)
                .OrderByDescending(msn => msn.SubmittedAt)
                .FirstOrDefaultAsync();

            var goalCompletions = await _context.MentorshipGoalCompletions
                .Where(mgc => mgc.MentorshipMatchId == matchId)
                .ToListAsync();
            var completionsForGoal = goalCompletions.Where(c => c.GoalId == goalId).ToList();
            var isCompletedByMentor = completionsForGoal.Any(c => c.CompletedByUserId == match.MentorId);
            var isCompletedByMentee = completionsForGoal.Any(c => c.CompletedByUserId == match.MenteeId);
            var isFullyCompleted = isCompletedByMentor && isCompletedByMentee;
            var completedAt = completionsForGoal.Any() ? completionsForGoal.Max(c => c.CompletedAt) : (DateTime?)null;

            var viewModel = new MenteeEvidenceFormViewModel
            {
                MatchId = matchId,
                GoalId = goalId,
                GoalName = goal.GoalName,
                GoalDescription = goal.GoalDescription,

                IsCompletedByMentor = isCompletedByMentor,
                IsCompletedByMentee = isCompletedByMentee,
                IsFullyCompleted = isFullyCompleted,
                CompletedAt = completedAt
            };

            if (mentorNote != null && mentorNote.IsTaskAssigned)
            {
                viewModel.IsTaskAssigned = true;
                viewModel.MentorTaskTitle = mentorNote.TaskTitle;
                viewModel.MentorTaskDescription = mentorNote.TaskDescription;
            }

            if(existingEvidence != null)
            {
                viewModel.Id = existingEvidence.Id;
                viewModel.WhatWasDone = existingEvidence.WhatWasDone;
                viewModel.AdditionalNotes = existingEvidence.AdditionalNotes;
                if (!string.IsNullOrEmpty(existingEvidence.EvidenceFilePaths))
                {
                    try
                    {
                        viewModel.ExistingEvidenceFilePaths = System.Text.Json.JsonSerializer
                            .Deserialize<List<string>>(existingEvidence.EvidenceFilePaths) ?? new List<string>();
                        // By default treat all existing files as retained (checkboxes will be checked)
                        viewModel.RetainedEvidenceFilePaths = new List<string>(viewModel.ExistingEvidenceFilePaths);
                    }
                    catch
                    {
                        viewModel.ExistingEvidenceFilePaths = new List<string>();
                        viewModel.RetainedEvidenceFilePaths = new List<string>();
                    }
                }
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitMenteeEvidence(MenteeEvidenceFormViewModel model)
        {
            _logger.LogInformation("SubmitMenteeEvidence called for MatchId={MatchId}, GoalId={GoalId}, EvidenceFilesCount={Count}",
                model?.MatchId, model?.GoalId, model?.EvidenceFiles?.Count ?? 0);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                             .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? (e.Exception?.Message ?? "Unknown error") : e.ErrorMessage)
                                             .ToList();

                _logger.LogWarning("SubmitMenteeEvidence ModelState invalid: {Errors}", string.Join(" | ", errors));
                if (errors.Any())
                    TempData["Error"] = errors.First();
                else
                    TempData["Error"] = "Form validation failed. Please check required fields.";

                var goal = await _context.Goals.FindAsync(model.GoalId);
                if (goal != null)
                {
                    model.GoalName = goal.GoalName;
                    model.GoalDescription = goal.GoalDescription;
                }
                return View(model);
            }

            var userId = GetCurrentUserId();
            var match = await _context.MentorshipMatches
                .FirstOrDefaultAsync(mm => mm.Id == model.MatchId && mm.MenteeId == userId && (mm.Status == "Active" || mm.Status == "Completed"));

            if (match == null)
            {
                TempData["Error"] = "Access denied or mentorship not found";
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }

            // Start with retained existing filenames (sent from the form)
            var finalFileNames = new List<string>();
            if (model.RetainedEvidenceFilePaths != null && model.RetainedEvidenceFilePaths.Any())
            {
                // view sends filenames only
                finalFileNames.AddRange(model.RetainedEvidenceFilePaths);
            }

            // Save newly uploaded files (persist file to disk with GUID prefix, but store filename only)
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".txt" };
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "mentorship-evidence");
            if (!Directory.Exists(uploadsDir))
                Directory.CreateDirectory(uploadsDir);

            if (model.EvidenceFiles != null && model.EvidenceFiles.Any())
            {
                foreach (var file in model.EvidenceFiles)
                {
                    if (file == null || file.Length == 0) continue;

                    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("EvidenceFiles", $"File {file.FileName} is not a valid file type.");
                        continue;
                    }
                    if (file.Length > 10 * 1024 * 1024)
                    {
                        ModelState.AddModelError("EvidenceFiles", $"File {file.FileName} is too large. Maximum size is 10MB.");
                        continue;
                    }

                    // store on disk as GUID_originalName to avoid collisions
                    var storedFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                    var filePath = Path.Combine(uploadsDir, storedFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Store only the filename in DB
                    finalFileNames.Add(storedFileName);
                }
            }

            // Attempt to find existing evidence entity by Id first, then by the unique key (MatchId+GoalId+UserId).
            MenteeSessionEvidence? evidenceEntity = null;
            if (model.Id.HasValue)
            {
                evidenceEntity = await _context.MenteeSessionEvidences.FindAsync(model.Id.Value);
            }

            if (evidenceEntity == null)
            {
                evidenceEntity = await _context.MenteeSessionEvidences
                    .FirstOrDefaultAsync(e => e.MentorshipMatchId == model.MatchId && e.GoalId == model.GoalId && e.UserId == userId);

                if (evidenceEntity != null)
                {
                    // ensure model.Id reflects existing DB entity (helps debugging/roundtrips)
                    model.Id = evidenceEntity.Id;
                }
            }

            // Track whether this operation will update an existing record or create a new one
            var isUpdate = evidenceEntity != null;

            if (evidenceEntity != null)
            {
                // Delete any server files that were not retained
                if (!string.IsNullOrEmpty(evidenceEntity.EvidenceFilePaths))
                {
                    try
                    {
                        var existingFileNames = System.Text.Json.JsonSerializer.Deserialize<List<string>>(evidenceEntity.EvidenceFilePaths) ?? new List<string>();
                        var toDelete = existingFileNames.Except(model.RetainedEvidenceFilePaths ?? new List<string>()).ToList();
                        foreach (var delFileName in toDelete)
                        {
                            try
                            {
                                var physical = Path.Combine(uploadsDir, delFileName);
                                if (System.IO.File.Exists(physical))
                                    System.IO.File.Delete(physical);
                            }
                            catch { /* swallow individual delete errors */ }
                        }
                    }
                    catch { /* ignore deserialization issues */ }
                }

                evidenceEntity.WhatWasDone = model.WhatWasDone;
                evidenceEntity.AdditionalNotes = model.AdditionalNotes;
                evidenceEntity.EvidenceFilePaths = finalFileNames.Any() ? System.Text.Json.JsonSerializer.Serialize(finalFileNames) : null;
                evidenceEntity.SubmittedAt = DateTime.UtcNow;

                _context.MenteeSessionEvidences.Update(evidenceEntity);
            }
            else
            {
                evidenceEntity = new MenteeSessionEvidence
                {
                    MentorshipMatchId = model.MatchId,
                    GoalId = model.GoalId,
                    UserId = userId,
                    WhatWasDone = model.WhatWasDone,
                    AdditionalNotes = model.AdditionalNotes,
                    EvidenceFilePaths = finalFileNames.Any() ? System.Text.Json.JsonSerializer.Serialize(finalFileNames) : null,
                    SubmittedAt = DateTime.UtcNow
                };
                _context.MenteeSessionEvidences.Add(evidenceEntity);
            }

            var evidenceSaved = false;
            try
            {
                await _context.SaveChangesAsync();
                // set success message based on update vs create
                TempData["Success"] = isUpdate ? "Notes updated successfully!" : "Notes submitted successfully!";
                evidenceSaved = true;
            }
            catch (DbUpdateException dbEx)
            {
                // Defensive: unique index race or unexpected duplicate — try to recover by updating the existing row.
                _logger.LogWarning(dbEx, "DbUpdateException while saving MenteeSessionEvidence. Attempting to recover by merging with existing record.");

                var existing = await _context.MenteeSessionEvidences
                    .FirstOrDefaultAsync(e => e.MentorshipMatchId == model.MatchId && e.GoalId == model.GoalId && e.UserId == userId);

                if (existing != null)
                {
                    // merge finalFileNames with existing stored file names (avoid losing files)
                    var existingFileNames = new List<string>();
                    if (!string.IsNullOrEmpty(existing.EvidenceFilePaths))
                    {
                        try { existingFileNames = System.Text.Json.JsonSerializer.Deserialize<List<string>>(existing.EvidenceFilePaths) ?? new List<string>(); } catch { existingFileNames = new List<string>(); }
                    }
                    var merged = existingFileNames.Union(finalFileNames).ToList();

                    existing.WhatWasDone = model.WhatWasDone;
                    existing.AdditionalNotes = model.AdditionalNotes;
                    existing.EvidenceFilePaths = merged.Any() ? System.Text.Json.JsonSerializer.Serialize(merged) : null;
                    existing.SubmittedAt = DateTime.UtcNow;

                    _context.MenteeSessionEvidences.Update(existing);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Notes updated successfully!";
                    evidenceSaved = true;
                }
                else
                {
                    // couldn't recover: rethrow so caller sees the error
                    throw;
                }
            }

            // Notify mentor that the mentee submitted/updated evidence/notes
            if (evidenceSaved)
            {
                try
                {
                    // Use the current user's full name claim if available (mentee who submitted)
                    var menteeName = User.FindFirst("FullName")?.Value ?? "Your mentee";
                    var goal = await _context.Goals.FindAsync(model.GoalId);
                    var goalName = !string.IsNullOrWhiteSpace(goal?.GoalName) ? goal!.GoalName : (model.GoalName ?? "the goal");

                    var notificationTitle = isUpdate ? "Mentee Notes Updated" : "New Mentee Notes";
                    var notificationMessage = $"{menteeName} has {(isUpdate ? "updated" : "submitted")} notes/work for \"{goalName}\". Please check them.";

                    var evidenceIconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path d=\"M10 12H14M12 10V14M19.9592 15H16.6C16.0399 15 15.7599 15 15.546 15.109C15.3578 15.2049 15.2049 15.3578 15.109 15.546C15 15.7599 15 16.0399 15 16.6V19.9592M20 14.1031V7.2C20 6.07989 20 5.51984 19.782 5.09202C19.5903 4.71569 19.2843 4.40973 18.908 4.21799C18.4802 4 17.9201 4 16.8 4H7.2C6.0799 4 5.51984 4 5.09202 4.21799C4.71569 4.40973 4.40973 4.71569 4.21799 5.09202C4 5.51984 4 6.0799 4 7.2V16.8C4 17.9201 4 18.4802 4.21799 18.908C4.40973 19.2843 4.71569 19.5903 5.09202 19.782C5.51984 20 6.0799 20 7.2 20H14.1031C14.5923 20 14.8369 20 15.067 19.9447C15.2711 19.8957 15.4662 19.8149 15.6451 19.7053C15.847 19.5816 16.0199 19.4086 16.3658 19.0627L19.0627 16.3658C19.4086 16.0199 19.5816 15.847 19.7053 15.6451C19.8149 15.4662 19.8957 15.2711 19.9447 15.067C20 14.8369 20 14.5923 20 14.1031Z\" stroke=\"#000000\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></path> </g></svg>";

                    var redirectUrl = $"/MentorshipManage/ViewMenteeSubmission?matchId={model.MatchId}&goalId={model.GoalId}";

                    await _notificationService.CreateNotificationAsync(
                        match.MentorId,
                        notificationTitle,
                        notificationMessage,
                        "mentee_evidence",
                        evidenceIconSvg,
                        redirectUrl
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create notification for mentor after mentee evidence submission.");
                    // don't fail the user flow if notification creation fails
                }
            }

            return RedirectToAction(nameof(SubmitMenteeEvidence), new { matchId = model.MatchId, goalId = model.GoalId });
        }

        [HttpGet]
        public async Task<IActionResult> SubmitMentorNote(Guid matchId, Guid goalId)
        {
            var userId = GetCurrentUserId();
            var match = await _context.MentorshipMatches
                .Include(mm => mm.Mentee)
                .FirstOrDefaultAsync(mm => mm.Id == matchId && mm.MentorId == userId && (mm.Status == "Active" || mm.Status == "Completed"));

            if (match == null)
            {
                TempData["Error"] = "Access denied or mentorship not found";
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }

            var goal = await _context.Goals
                .FirstOrDefaultAsync(g => g.Id == goalId && g.IsActive);

            if (goal == null)
            {
                TempData["Error"] = "Goal not found";
                return RedirectToAction("Goals", new { matchId });
            }

            // If mentor already has a note for this match+goal return the populated form for editing
            var existingNote = await _context.MentorSessionNotes
                .FirstOrDefaultAsync(msn => msn.MentorshipMatchId == matchId &&
                                           msn.GoalId == goalId &&
                                           msn.MentorId == userId);

            var goalCompletions = await _context.MentorshipGoalCompletions
                .Where(mgc => mgc.MentorshipMatchId == matchId)
                .ToListAsync();
            var completionsForGoal = goalCompletions.Where(c => c.GoalId == goalId).ToList();
            var isCompletedByMentor = completionsForGoal.Any(c => c.CompletedByUserId == match.MentorId);
            var isCompletedByMentee = completionsForGoal.Any(c => c.CompletedByUserId == match.MenteeId);
            var isFullyCompleted = isCompletedByMentor && isCompletedByMentee;
            var completedAt = completionsForGoal.Any() ? completionsForGoal.Max(c => c.CompletedAt) : (DateTime?)null;

            var viewModel = new MentorNoteFormViewModel
            {
                MatchId = matchId,
                GoalId = goalId,
                GoalName = goal.GoalName,
                GoalDescription = goal.GoalDescription,
                MenteeName = $"{match.Mentee.FirstName} {match.Mentee.LastName}",

                IsCompletedByMentor = isCompletedByMentor,
                IsCompletedByMentee = isCompletedByMentee,
                IsFullyCompleted = isFullyCompleted,
                CompletedAt = completedAt
            };

            if (existingNote != null)
            {
                // populate for editing
                viewModel.Id = existingNote.Id;
                viewModel.Notes = existingNote.Notes;
                viewModel.Feedback = existingNote.Feedback;
                viewModel.ProgressRating = existingNote.ProgressRating;
                viewModel.IsTaskAssigned = existingNote.IsTaskAssigned;
                viewModel.TaskTitle = existingNote.TaskTitle;
                viewModel.TaskDescription = existingNote.TaskDescription;
            }

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitMentorNote(MentorNoteFormViewModel model)
        {
            _logger.LogInformation("SubmitMentorNote called for MatchId={MatchId}, GoalId={GoalId}", model?.MatchId, model?.GoalId);

            // Basic server-side conditional validation: if task assigned require title/description
            if (model.IsTaskAssigned)
            {
                if (string.IsNullOrWhiteSpace(model.TaskTitle))
                    ModelState.AddModelError(nameof(model.TaskTitle), "Task title is required when assigning a task.");
                if (string.IsNullOrWhiteSpace(model.TaskDescription))
                    ModelState.AddModelError(nameof(model.TaskDescription), "Task description is required when assigning a task.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                             .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? (e.Exception?.Message ?? "Unknown error") : e.ErrorMessage)
                                             .ToList();

                _logger.LogWarning("SubmitMentorNote ModelState invalid: {Errors}", string.Join(" | ", errors));

                if (errors.Any())
                    TempData["Error"] = errors.First();
                else
                    TempData["Error"] = "Form validation failed. Please check required fields.";

                var mentorshipMatch = await _context.MentorshipMatches
                    .Include(mm => mm.Mentee)
                    .FirstOrDefaultAsync(mm => mm.Id == model.MatchId);
                var goal = await _context.Goals.FindAsync(model.GoalId);

                if (goal != null)
                {
                    model.GoalName = goal.GoalName;
                    model.GoalDescription = goal.GoalDescription;
                }
                if (mentorshipMatch != null)
                {
                    model.MenteeName = $"{mentorshipMatch.Mentee.FirstName} {mentorshipMatch.Mentee.LastName}";
                }
                return View(model);
            }

            var userId = GetCurrentUserId();
            var match = await _context.MentorshipMatches
                .FirstOrDefaultAsync(mm => mm.Id == model.MatchId && mm.MentorId == userId && (mm.Status == "Active" || mm.Status == "Completed"));

            if (match == null)
            {
                TempData["Error"] = "Access denied or mentorship not found";
                return RedirectToAction("AvailableMentors", "MentorshipMatching");
            }

            // Check if a note already exists — update it, otherwise create new
            var existingNote = await _context.MentorSessionNotes
                .FirstOrDefaultAsync(msn => msn.MentorshipMatchId == model.MatchId &&
                                           msn.GoalId == model.GoalId &&
                                           msn.MentorId == userId);

            var noteWasSaved = false;
            if (existingNote != null)
            {
                existingNote.Notes = model.Notes;
                existingNote.Feedback = model.Feedback;
                existingNote.ProgressRating = model.ProgressRating;
                existingNote.IsTaskAssigned = model.IsTaskAssigned;
                existingNote.TaskTitle = model.IsTaskAssigned ? model.TaskTitle : null;
                existingNote.TaskDescription = model.IsTaskAssigned ? model.TaskDescription : null;
                existingNote.SubmittedAt = DateTime.UtcNow;

                _context.MentorSessionNotes.Update(existingNote);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Notes updated successfully!";
                noteWasSaved = true;
            }
            else
            {
                var note = new MentorSessionNote
                {
                    MentorshipMatchId = model.MatchId,
                    GoalId = model.GoalId,
                    MentorId = userId,
                    Notes = model.Notes,
                    Feedback = model.Feedback,
                    ProgressRating = model.ProgressRating,
                    IsTaskAssigned = model.IsTaskAssigned,
                    TaskTitle = model.IsTaskAssigned ? model.TaskTitle : null,
                    TaskDescription = model.IsTaskAssigned ? model.TaskDescription : null,
                    SubmittedAt = DateTime.UtcNow
                };

                _context.MentorSessionNotes.Add(note);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Notes submitted successfully!";
                noteWasSaved = true;
            }

            if (noteWasSaved)
            {
                try
                {
                    var mentorName = User.FindFirst("FullName")?.Value ?? "Your mentor";
                    var goal = await _context.Goals.FindAsync(model.GoalId);
                    var goalName = !string.IsNullOrWhiteSpace(goal?.GoalName) ? goal!.GoalName : (model.GoalName ?? "the goal");

                    var notificationTitle = existingNote != null ? "Mentor Note Updated" : "New Mentor Note";
                    var notificationMessage = $"{mentorName} has {(existingNote != null ? "updated" : "submitted")} session notes/task for \"{goalName}\".";

                    var noteIconSvg = "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path d=\"M10 12H14M12 10V14M19.9592 15H16.6C16.0399 15 15.7599 15 15.546 15.109C15.3578 15.2049 15.2049 15.3578 15.109 15.546C15 15.7599 15 16.0399 15 16.6V19.9592M20 14.1031V7.2C20 6.07989 20 5.51984 19.782 5.09202C19.5903 4.71569 19.2843 4.40973 18.908 4.21799C18.4802 4 17.9201 4 16.8 4H7.2C6.0799 4 5.51984 4 5.09202 4.21799C4.71569 4.40973 4.40973 4.71569 4.21799 5.09202C4 5.51984 4 6.0799 4 7.2V16.8C4 17.9201 4 18.4802 4.21799 18.908C4.40973 19.2843 4.71569 19.5903 5.09202 19.782C5.51984 20 6.0799 20 7.2 20H14.1031C14.5923 20 14.8369 20 15.067 19.9447C15.2711 19.8957 15.4662 19.8149 15.6451 19.7053C15.847 19.5816 16.0199 19.4086 16.3658 19.0627L19.0627 16.3658C19.4086 16.0199 19.5816 15.847 19.7053 15.6451C19.8149 15.4662 19.8957 15.2711 19.9447 15.067C20 14.8369 20 14.5923 20 14.1031Z\" stroke=\"#000000\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></path> </g></svg>";

                    var redirectUrl = $"/MentorshipManage/ViewMentorNotes?matchId={model.MatchId}&goalId={model.GoalId}";

                    await _notificationService.CreateNotificationAsync(
                        match.MenteeId,
                        notificationTitle,
                        notificationMessage,
                        "mentor_note",
                        noteIconSvg,
                        redirectUrl
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create notification for mentee after mentor note submission.");
                    // don't fail the user flow if notification creation fails
                }
            }

            return RedirectToAction(nameof(SubmitMentorNote), new { matchId = model.MatchId, goalId = model.GoalId });
        }

        [HttpGet]
        public async Task<IActionResult> ViewMentorNotes(Guid matchId, Guid goalId)
        {
            var userId = GetCurrentUserId();

            // Allow both mentor and mentee to view mentor notes for the match
            var match = await _context.MentorshipMatches
                .Include(mm => mm.Mentor)
                .Include(mm => mm.Mentee)
                .FirstOrDefaultAsync(mm => mm.Id == matchId && (mm.MentorId == userId || mm.MenteeId == userId) && (mm.Status == "Active" || mm.Status == "Completed"));

            if (match == null)
            {
                TempData["Error"] = "Access denied or mentorship not found";
                return RedirectToAction("Goals", new { matchId });
            }

            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == goalId && g.IsActive);
            if (goal == null)
            {
                TempData["Error"] = "Goal not found";
                return RedirectToAction("Goals", new { matchId });
            }

            var mentorNote = await _context.MentorSessionNotes
                .Where(msn => msn.MentorshipMatchId == matchId && msn.GoalId == goalId)
                .OrderByDescending(msn => msn.SubmittedAt)
                .FirstOrDefaultAsync();

            var vm = new Freelancing.Models.MentorNoteDisplay
            {
                MatchId = matchId,
                GoalId = goalId,
                GoalName = goal.GoalName,
                MentorName = $"{match.Mentor.FirstName} {match.Mentor.LastName}",
                MenteeName = $"{match.Mentee.FirstName} {match.Mentee.LastName}"
            };

            if (mentorNote != null)
            {
                vm.Id = mentorNote.Id;
                vm.Notes = mentorNote.Notes;
                vm.Feedback = mentorNote.Feedback;
                vm.ProgressRating = mentorNote.ProgressRating;
                vm.IsTaskAssigned = mentorNote.IsTaskAssigned;
                vm.TaskTitle = mentorNote.TaskTitle;
                vm.TaskDescription = mentorNote.TaskDescription;
                vm.SubmittedAt = mentorNote.SubmittedAt;
            }

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> ViewMenteeSubmission(Guid matchId, Guid goalId)
        {
            var userId = GetCurrentUserId();

            // Only mentor may view this endpoint
            var match = await _context.MentorshipMatches
                .Include(mm => mm.Mentee)
                .FirstOrDefaultAsync(mm => mm.Id == matchId && mm.MentorId == userId && (mm.Status == "Active" || mm.Status == "Completed"));

            if (match == null)
            {
                TempData["Error"] = "Access denied or mentorship not found";
                return RedirectToAction("Goals", new { matchId });
            }

            var goal = await _context.Goals.FirstOrDefaultAsync(g => g.Id == goalId && g.IsActive);
            if (goal == null)
            {
                TempData["Error"] = "Goal not found";
                return RedirectToAction("Goals", new { matchId });
            }

            var evidences = await _context.MenteeSessionEvidences
                .Where(e => e.MentorshipMatchId == matchId && e.GoalId == goalId)
                .OrderByDescending(e => e.SubmittedAt)
                .ToListAsync();

            var mentorNote = await _context.MentorSessionNotes
                .Where(msn => msn.MentorshipMatchId == matchId && msn.GoalId == goalId)
                .OrderByDescending(msn => msn.SubmittedAt)
                .FirstOrDefaultAsync();

            var goalCompletions = await _context.MentorshipGoalCompletions
                .Where(mgc => mgc.MentorshipMatchId == matchId)
                .ToListAsync();
            var completionsForGoal = goalCompletions.Where(c => c.GoalId == goalId).ToList();
            var isCompletedByMentor = completionsForGoal.Any(c => c.CompletedByUserId == match.MentorId);
            var isCompletedByMentee = completionsForGoal.Any(c => c.CompletedByUserId == match.MenteeId);
            var isFullyCompleted = isCompletedByMentor && isCompletedByMentee;
            var completedAt = completionsForGoal.Any() ? completionsForGoal.Max(c => c.CompletedAt) : (DateTime?)null;

            var vm = new Models.MenteeEvidenceDisplayViewModel
            {
                MatchId = matchId,
                GoalId = goalId,
                GoalName = goal.GoalName,
                MenteeName = $"{match.Mentee.FirstName} {match.Mentee.LastName}",
                Evidences = evidences,

                IsCompletedByMentor = isCompletedByMentor,
                IsCompletedByMentee = isCompletedByMentee,
                IsFullyCompleted = isFullyCompleted,
                CompletedAt = completedAt
            };

            if (mentorNote != null && mentorNote.IsTaskAssigned)
            {
                vm.IsTaskAssigned = true;
                vm.TaskTitle = mentorNote.TaskTitle;
                vm.TaskDescription = mentorNote.TaskDescription;
            }

            return View(vm);
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

            // Enforce: only mentor can mark from this flow
            if (match.MentorId != userId)
            {
                TempData["Error"] = "Only the mentor can mark this goal as complete from this view.";
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

            // Ensure required forms have been submitted
            var menteeEvidence = await _context.MenteeSessionEvidences
                .FirstOrDefaultAsync(mse => mse.MentorshipMatchId == matchId && mse.GoalId == goalId && mse.UserId == match.MenteeId);

            if (menteeEvidence == null)
            {
                TempData["Error"] = "The mentee must submit evidence before you can mark this goal as complete.";
                return RedirectToAction("Goals", new { matchId });
            }

            var mentorNote = await _context.MentorSessionNotes
                .FirstOrDefaultAsync(msn => msn.MentorshipMatchId == matchId && msn.GoalId == goalId && msn.MentorId == userId);

            if (mentorNote == null)
            {
                TempData["Error"] = "You must submit session notes before marking this goal as complete.";
                return RedirectToAction("SubmitMentorNote", new { matchId, goalId });
            }

            // Check previous goal completion constraint (unchanged)
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

            // Avoid duplicate mentor completion
            var existingMentorCompletion = await _context.MentorshipGoalCompletions
                .FirstOrDefaultAsync(mgc => mgc.MentorshipMatchId == matchId && mgc.GoalId == goalId && mgc.CompletedByUserId == match.MentorId);

            if (existingMentorCompletion != null)
            {
                TempData["Error"] = "You have already marked this goal as done";
                return RedirectToAction("Goals", new { matchId });
            }

            var now = DateTime.UtcNow;

            // Create mentor completion record
            var mentorCompletion = new MentorshipGoalCompletion
            {
                Id = Guid.NewGuid(),
                MentorshipMatchId = matchId,
                GoalId = goalId,
                CompletedByUserId = match.MentorId,
                CompletionType = "Mentor",
                CompletedAt = now,
                MenteeEvidenceId = menteeEvidence?.Id,
                MentorNoteId = mentorNote?.Id,
                IsCompletedByMentor = true,
                IsCompletedByMentee = false
            };

            _context.MentorshipGoalCompletions.Add(mentorCompletion);

            // Ensure mentee completion record exists (so UI sees both sides completed)
            var existingMenteeCompletion = await _context.MentorshipGoalCompletions
                .FirstOrDefaultAsync(mgc => mgc.MentorshipMatchId == matchId && mgc.GoalId == goalId && mgc.CompletedByUserId == match.MenteeId);

            if (existingMenteeCompletion == null)
            {
                var menteeCompletion = new MentorshipGoalCompletion
                {
                    Id = Guid.NewGuid(),
                    MentorshipMatchId = matchId,
                    GoalId = goalId,
                    CompletedByUserId = match.MenteeId,
                    CompletionType = "Mentee",
                    CompletedAt = now,
                    MenteeEvidenceId = menteeEvidence?.Id,
                    MentorNoteId = mentorNote?.Id,
                    IsCompletedByMentor = false,
                    IsCompletedByMentee = true
                };
                _context.MentorshipGoalCompletions.Add(menteeCompletion);
            }
            else
            {
                // ensure flags are set
                if (!existingMenteeCompletion.IsCompletedByMentee)
                {
                    existingMenteeCompletion.IsCompletedByMentee = true;
                    _context.MentorshipGoalCompletions.Update(existingMenteeCompletion);
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Goal marked as done successfully";

            // Notify both mentor and mentee that the goal is complete and they may move to the next goal
            try
            {
                var actorName = User.FindFirst("FullName")?.Value ?? "Your mentor";
                var goalName = !string.IsNullOrWhiteSpace(goal?.GoalName) ? goal!.GoalName : "the goal";
                var notificationTitle = "Goal Completed";
                var notificationMessage = $"{actorName} marked \"{goalName}\" complete. You may move to the next goal!";
                var finishIconSvg = "<svg viewBox='0 0 20 20' version='1.1' xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' fill='#000000'><g id='SVGRepo_bgCarrier' stroke-width='0'></g><g id='SVGRepo_tracerCarrier' stroke-linecap='round' stroke-linejoin='round'></g><g id='SVGRepo_iconCarrier'> <title>finish_line [#103]</title> <desc>Created with Sketch.</desc> <defs> </defs> <g id='Page-1' stroke='none' stroke-width='1' fill='none' fill-rule='evenodd'> <g id='Dribbble-Light-Preview' transform='translate(-260.000000, -7759.000000)' fill='#000000'> <g id='icons' transform='translate(56.000000, 160.000000)'> <path d='M214,7611 L218,7611 L218,7607 L214,7607 L214,7611 Z M210,7607 L214,7607 L214,7603 L210,7603 L210,7607 Z M214,7603 L218,7603 L218,7599 L214,7599 L214,7603 Z M222,7599 L222,7603 L218,7603 L218,7607 L222,7607 L222,7611 L224,7611 L224,7599 L222,7599 Z M206,7607 L210,7607 L210,7611 L206,7611 L206,7619 L204,7619 L204,7599 L210,7599 L210,7603 L206,7603 L206,7607 Z' id='finish_line-[#103]'> </path> </g> </g> </g> </g></svg>";

                var redirectUrl = $"/MentorshipManage/Goals?matchId={matchId}";

                // notify mentor
                await _notificationService.CreateNotificationAsync(
                    match.MentorId,
                    notificationTitle,
                    notificationMessage,
                    "goal_completed",
                    finishIconSvg,
                    redirectUrl
                );

                // notify mentee
                await _notificationService.CreateNotificationAsync(
                    match.MenteeId,
                    notificationTitle,
                    notificationMessage,
                    "goal_completed",
                    finishIconSvg,
                    redirectUrl
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create completion notifications after marking goal done (matchId={MatchId}, goalId={GoalId}).", matchId, goalId);
                // do not fail the user flow if notifications fail
            }

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

            var viewModel = new MentorFeedbackViewModel
            {
                MatchId = matchId,
                MentorName = $"{match.Mentor.FirstName} {match.Mentor.LastName}",
                MentorPhoto = match.Mentor.Photo,
                MenteeName = $"{match.Mentee.FirstName} {match.Mentee.LastName}",
                MatchStartDate = match.StartDate ?? match.MatchedDate,
                MatchEndDate = match.EndDate
            };

            // If review exists, populate the form with existing data
            if (existingReview != null)
            {
                viewModel.Rating = existingReview.Rating;
                viewModel.WouldRecommend = existingReview.WouldRecommend;
                viewModel.Comments = existingReview.Comments;
                viewModel.Strengths = existingReview.Strengths;
                viewModel.AreasForImprovement = existingReview.AreasForImprovement;

                // Add a flag to indicate this is an existing review
                ViewBag.IsExistingReview = true;
                ViewBag.ReviewSubmittedDate = existingReview.CreatedAt.ToString("MMM dd, yyyy 'at' h:mm tt");
            }
            else
            {
                ViewBag.IsExistingReview = false;
            }

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

            if (!ModelState.IsValid)
            {
                // Repopulate the view model with mentor info
                model.MentorName = $"{match.Mentor.FirstName} {match.Mentor.LastName}";
                model.MentorPhoto = match.Mentor.Photo;
                model.MenteeName = $"{match.Mentee.FirstName} {match.Mentee.LastName}";
                model.MatchStartDate = match.StartDate ?? match.MatchedDate;
                model.MatchEndDate = match.EndDate;

                ViewBag.IsExistingReview = existingReview != null;
                if (existingReview != null)
                {
                    ViewBag.ReviewSubmittedDate = existingReview.CreatedAt.ToString("MMM dd, yyyy 'at' h:mm tt");
                }

                return View(model);
            }

            if (existingReview != null)
            {
                // Update existing review
                existingReview.Rating = model.Rating;
                existingReview.WouldRecommend = model.WouldRecommend;
                existingReview.Comments = model.Comments;
                existingReview.Strengths = model.Strengths;
                existingReview.AreasForImprovement = model.AreasForImprovement;
                existingReview.CreatedAt = DateTime.UtcNow; // Update timestamp

                _context.MentorReviews.Update(existingReview);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Your feedback has been updated successfully.";
            }
            else
            {
                // Create new review
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
                    CreatedAt = DateTime.UtcNow
                };

                _context.MentorReviews.Add(review);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Thank you for your feedback! Your review has been submitted successfully.";

                // Send notification to the mentor about the review
                await _notificationService.CreateNotificationAsync(
                    match.MentorId,
                    "New Review Received",
                    $"{match.Mentee.FirstName} {match.Mentee.LastName} has submitted a review for your mentorship.",
                    "mentor_review",
                    "<svg viewBox=\"0 0 24 24\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\"><g id=\"SVGRepo_bgCarrier\" stroke-width=\"0\"></g><g id=\"SVGRepo_tracerCarrier\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></g><g id=\"SVGRepo_iconCarrier\"> <path d=\"M16 1C17.6569 1 19 2.34315 19 4C19 4.55228 18.5523 5 18 5C17.4477 5 17 4.55228 17 4C17 3.44772 16.5523 3 16 3H4C3.44772 3 3 3.44772 3 4V20C3 20.5523 3.44772 21 4 21H16C16.5523 21 17 20.5523 17 20V19C17 18.4477 17.4477 18 18 18C18.5523 18 19 18.4477 19 19V20C19 21.6569 17.6569 23 16 23H4C2.34315 23 1 21.6569 1 20V4C1 2.34315 2.34315 1 4 1H16Z\" fill=\"#0F0F0F\"></path> <path fill-rule=\"evenodd\" clip-rule=\"evenodd\" d=\"M20.7991 8.20087C20.4993 7.90104 20.0132 7.90104 19.7133 8.20087L11.9166 15.9977C11.7692 16.145 11.6715 16.3348 11.6373 16.5404L11.4728 17.5272L12.4596 17.3627C12.6652 17.3285 12.855 17.2308 13.0023 17.0835L20.7991 9.28666C21.099 8.98682 21.099 8.5007 20.7991 8.20087ZM18.2991 6.78666C19.38 5.70578 21.1325 5.70577 22.2134 6.78665C23.2942 7.86754 23.2942 9.61999 22.2134 10.7009L14.4166 18.4977C13.9744 18.9398 13.4052 19.2327 12.7884 19.3355L11.8016 19.5C10.448 19.7256 9.2744 18.5521 9.50001 17.1984L9.66448 16.2116C9.76728 15.5948 10.0602 15.0256 10.5023 14.5834L18.2991 6.78666Z\" fill=\"#0F0F0F\"></path> <path d=\"M5 7C5 6.44772 5.44772 6 6 6H14C14.5523 6 15 6.44772 15 7C15 7.55228 14.5523 8 14 8H6C5.44772 8 5 7.55228 5 7Z\" fill=\"#0F0F0F\"></path> <path d=\"M5 11C5 10.4477 5.44772 10 6 10H10C10.5523 10 11 10.4477 11 11C11 11.5523 10.5523 12 10 12H6C5.44772 12 5 11.5523 5 11Z\" fill=\"#0F0F0F\"></path> <path d=\"M5 15C5 14.4477 5.44772 14 6 14H7C7.55228 14 8 14.4477 8 15C8 15.5523 7.55228 16 7 16H6C5.44772 16 5 15.5523 5 15Z\" fill=\"#0F0F0F\"></path> </g></svg>",
                    "/MentorshipManage/MyReviews"
                );
            }

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
