using Freelancing.Data;
using Freelancing.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace Freelancing.Services
{
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPdfService _pdfService;
        private readonly IServiceProvider _serviceProvider;

        public ReportService(
            ApplicationDbContext context,
            IPdfService pdfService,
            IServiceProvider serviceProvider)
        {
            _context = context;
            _pdfService = pdfService;
            _serviceProvider = serviceProvider;
        }

        public async Task<byte[]> GenerateFreelancerPerformanceReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-12);
            endDate ??= DateTime.UtcNow;

            var freelancer = await _context.UserAccounts
                .Include(u => u.UserAccountSkills)
                .ThenInclude(uas => uas.UserSkill)
                .FirstOrDefaultAsync(u => u.Id == userId);

            var biddings = await _context.Biddings
                .Include(b => b.Project)
                .ThenInclude(p => p.User)
                .Where(b => b.UserId == userId &&
                           b.Project.CreatedAt >= startDate &&
                           b.Project.CreatedAt <= endDate)
                .ToListAsync();

            var feedbacks = await _context.FreelancerFeedbacks
                .Include(f => f.AcceptBidding)
                .ThenInclude(ab => ab.Project)
                .Where(f => f.FreelancerId == userId &&
                           f.CreatedAt >= startDate &&
                           f.CreatedAt <= endDate)
                .ToListAsync();

            // Get comparative data from platform
            var platformAvgRating = await _context.FreelancerFeedbacks
                .Where(f => f.CreatedAt >= startDate && f.CreatedAt <= endDate)
                .AverageAsync(f => (double?)f.Rating) ?? 0;

            var platformAvgSuccessRate = await CalculatePlatformSuccessRateAsync(startDate.Value, endDate.Value);

            var reportData = new FreelancerReportData
            {
                Freelancer = freelancer,
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                Biddings = biddings,
                Feedbacks = feedbacks,
                PlatformAverageRating = platformAvgRating,
                PlatformAverageSuccessRate = platformAvgSuccessRate
            };

            var htmlContent = GenerateFreelancerPerformanceHtml(reportData);
            return await _pdfService.GenerateHtmlToPdfAsync(htmlContent, "Freelancer Performance Report");
        }

        public async Task<byte[]> GenerateClientProjectReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-12);
            endDate ??= DateTime.UtcNow;

            var client = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId);

            var projects = await _context.Projects
                .Include(p => p.AcceptedBid)
                .ThenInclude(ab => ab.User)
                .Include(p => p.Biddings)
                .Where(p => p.UserId == userId &&
                           p.CreatedAt >= startDate &&
                           p.CreatedAt <= endDate)
                .ToListAsync();

            var sentReviews = await _context.FreelancerFeedbacks
                .Include(f => f.AcceptBidding)
                .ThenInclude(ab => ab.Project)
                .Include(f => f.Freelancer)
                .Where(f => f.AcceptBidding.Project.UserId == userId &&
                           f.CreatedAt >= startDate &&
                           f.CreatedAt <= endDate)
                .ToListAsync();

            // Get platform benchmarks
            var platformAvgBudget = await _context.Projects
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate && p.AcceptedBid != null)
                .AverageAsync(p => (double?)p.AcceptedBid.Budget) ?? 0;

            var platformAvgCompletionRate = await CalculatePlatformCompletionRateAsync(startDate.Value, endDate.Value);

            var reportData = new ClientReportData
            {
                Client = client,
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                Projects = projects,
                SentReviews = sentReviews,
                PlatformAverageBudget = platformAvgBudget,
                PlatformAverageCompletionRate = platformAvgCompletionRate
            };

            var htmlContent = GenerateClientProjectHtml(reportData);
            return await _pdfService.GenerateHtmlToPdfAsync(htmlContent, "Client Project Report");
        }

        public async Task<byte[]> GenerateAdminSystemReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-3);
            endDate ??= DateTime.UtcNow;

            // Calculate trends by comparing with previous period
            var periodLength = (endDate.Value - startDate.Value).Days;
            var previousStartDate = startDate.Value.AddDays(-periodLength);
            var previousEndDate = startDate.Value;

            // Get delayed/late projects
            var delayedProjects = await _context.Projects
                .Include(p => p.AcceptedBid)
                .ThenInclude(ab => ab.User)
                .Include(p => p.User)
                .Include(p => p.Contract)
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate &&
                           p.Deadline.HasValue &&
                           ((p.Status == "Active" && DateTime.UtcNow > p.Deadline.Value) ||
                            (p.Status == "Completed" && p.Contract != null &&
                             p.Contract.CompletedAt.HasValue &&
                             p.Contract.CompletedAt.Value > p.Deadline.Value)))
                .ToListAsync();

            var reportData = new SystemReportData
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,

                // Current period metrics
                TotalUsers = await _context.UserAccounts.CountAsync(),
                TotalFreelancers = await _context.UserAccounts.CountAsync(u => u.Role == "Freelancer" || u.FRole == "Freelancer"),
                TotalClients = await _context.UserAccounts.CountAsync(u => u.Role == "Client" || u.FRole == "Client"),
                TotalProjects = await _context.Projects.CountAsync(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate),
                ActiveProjects = await _context.Projects.CountAsync(p => p.Status == "Active"),
                CompletedProjects = await _context.Projects.CountAsync(p => p.Status == "Completed" && p.CreatedAt >= startDate && p.CreatedAt <= endDate),
                TotalBiddings = await _context.Biddings.CountAsync(b => b.Project.CreatedAt >= startDate && b.Project.CreatedAt <= endDate),
                AcceptedBiddings = await _context.Biddings.CountAsync(b => b.IsAccepted && b.Project.CreatedAt >= startDate && b.Project.CreatedAt <= endDate),
                TotalMentorshipMatches = await _context.MentorshipMatches.CountAsync(),
                ActiveMentorships = await _context.MentorshipMatches.CountAsync(m => m.Status == "Active"),

                // Previous period for comparison
                PreviousPeriodProjects = await _context.Projects.CountAsync(p => p.CreatedAt >= previousStartDate && p.CreatedAt < previousEndDate),
                PreviousPeriodBiddings = await _context.Biddings.CountAsync(b => b.Project.CreatedAt >= previousStartDate && b.Project.CreatedAt < previousEndDate),
                PreviousPeriodCompletedProjects = await _context.Projects.CountAsync(p => p.Status == "Completed" && p.CreatedAt >= previousStartDate && p.CreatedAt < previousEndDate),

                // Problem areas
                InactiveFreelancers = await _context.UserAccounts
                    .Where(u => (u.Role == "Freelancer" || u.FRole == "Freelancer") &&
                           !_context.Biddings.Any(b => b.UserId == u.Id && b.Project.CreatedAt >= startDate))
                    .CountAsync(),

                ProjectsWithoutBids = await _context.Projects
                    .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate && !p.Biddings.Any())
                    .CountAsync(),

                LowRatedFreelancers = await _context.FreelancerFeedbacks
                    .Where(f => f.CreatedAt >= startDate && f.CreatedAt <= endDate)
                    .GroupBy(f => f.FreelancerId)
                    .Where(g => g.Average(f => f.Rating) < 3)
                    .CountAsync(),

                // NEW: Delayed/Late projects tracking
                DelayedProjects = delayedProjects.Where(p => p.Status == "Active").ToList(),
                LateCompletedProjects = delayedProjects.Where(p => p.Status == "Completed").ToList()
            };

            var htmlContent = GenerateSystemReportHtml(reportData);
            return await _pdfService.GenerateHtmlToPdfAsync(htmlContent, "System Analytics Report");
        }

        public async Task<byte[]> GenerateFinancialReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-12);
            endDate ??= DateTime.UtcNow;

            var freelancer = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId);

            var completedBiddings = await _context.Biddings
                .Include(b => b.Project)
                .ThenInclude(p => p.User)
                .Where(b => b.UserId == userId &&
                           b.IsAccepted &&
                           b.Project.Status == "Completed" &&
                           b.BiddingAcceptedDate >= startDate &&
                           b.BiddingAcceptedDate <= endDate)
                .ToListAsync();

            var reportData = new FinancialReportData
            {
                Freelancer = freelancer,
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                CompletedBiddings = completedBiddings
            };

            var htmlContent = GenerateFinancialReportHtml(reportData);
            return await _pdfService.GenerateHtmlToPdfAsync(htmlContent, "Financial Report");
        }

        public async Task<byte[]> GenerateFreelancerBidsReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-12);
            endDate ??= DateTime.UtcNow;

            var freelancer = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.Id == userId);

            var biddings = await _context.Biddings
                .Include(b => b.Project)
                .ThenInclude(p => p.User)
                .Where(b => b.UserId == userId &&
                           b.Project.CreatedAt >= startDate &&
                           b.Project.CreatedAt <= endDate)
                .ToListAsync();

            var acceptedBids = biddings.Where(b => b.IsAccepted).ToList();
            var successRate = biddings.Any() ? (acceptedBids.Count * 100.0 / biddings.Count) : 0;

            var headerHtml = GenerateReportHeader(
                "Bids and Acceptance Rate Report",
                $"For {freelancer?.FirstName} {freelancer?.LastName}",
                startDate.Value,
                endDate.Value,
                "#3B82F6"
            );

            return await _pdfService.GenerateHtmlToPdfAsync($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Bids and Acceptance Rate Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #3B82F6; }}
        .main-header {{ border-bottom-color: #3B82F6; }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>Performance Overview</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{biddings.Count}</span>
            <div class='kpi-label'>Total Bids</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{acceptedBids.Count}</span>
            <div class='kpi-label'>Accepted Bids</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{successRate:F1}%</span>
            <div class='kpi-label'>Success Rate</div>
        </div>
    </div>

    <h2>Bid Details</h2>
    {(biddings.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Project Name</th>
                <th>Client</th>
                <th class='text-right'>Bid Amount (PHP)</th>
                <th>Delivery (Days)</th>
                <th>Proposal</th>
                <th>Date Submitted</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", biddings.OrderByDescending(b => b.Project.CreatedAt).Select(b => $@"
            <tr>
                <td>{b.Project.ProjectName}</td>
                <td>{b.Project.User.FirstName} {b.Project.User.LastName}</td>
                <td class='text-right'>PHP {b.Budget:N0}</td>
                <td>{b.Delivery}</td>
                <td class='review-comment'>{(string.IsNullOrEmpty(b.Proposal) ? "—" : b.Proposal)}</td>
                <td>{b.Project.CreatedAt:MMM dd, yyyy}</td>
            </tr>"))}
        </tbody>
    </table>" : "<p>No bids submitted in this period.</p>")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>", "Bids and Acceptance Rate Report");
        }

        public async Task<byte[]> GenerateFreelancerReviewsReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-12);
            endDate ??= DateTime.UtcNow;

            var freelancer = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.Id == userId);

            var feedbacks = await _context.FreelancerFeedbacks
                .Include(f => f.AcceptBidding)
                .ThenInclude(ab => ab.Project)
                .ThenInclude(p => p.User)
                .Where(f => f.FreelancerId == userId &&
                           f.CreatedAt >= startDate &&
                           f.CreatedAt <= endDate)
                .ToListAsync();

            var averageRating = feedbacks.Any() ? feedbacks.Average(f => f.Rating) : 0;
            var recommendationRate = feedbacks.Any() ?
                (feedbacks.Count(f => f.WouldRecommend) * 100.0 / feedbacks.Count) : 0;

            var headerHtml = GenerateReportHeader(
                "Client Reviews Report",
                $"For {freelancer?.FirstName} {freelancer?.LastName}",
                startDate.Value,
                endDate.Value,
                "#3B82F6"
            );

            return await _pdfService.GenerateHtmlToPdfAsync($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Client Reviews Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #3B82F6; }}
        .main-header {{ border-bottom-color: #3B82F6; }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>Performance Overview</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{feedbacks.Count}</span>
            <div class='kpi-label'>Reviews Received</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{averageRating:F1} ⭐</span>
            <div class='kpi-label'>Average Rating</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{recommendationRate:F1}%</span>
            <div class='kpi-label'>Recommendation Rate</div>
        </div>
    </div>

    <h2>Client Reviews Received</h2>
    {(feedbacks.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Project</th>
                <th>Client</th>
                <th>Rating</th>
                <th>Recommend</th>
                <th>Comments</th>
                <th>Date</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", feedbacks.OrderByDescending(f => f.CreatedAt).Select(f => $@"
            <tr>
                <td>{f.AcceptBidding.Project.ProjectName}</td>
                <td>{f.AcceptBidding.Project.User.FirstName} {f.AcceptBidding.Project.User.LastName}</td>
                <td class='star-rating'>{f.Rating}/5</td>
                <td>{(f.WouldRecommend ? "Yes" : "No")}</td>
                <td class='review-comment'>{(string.IsNullOrEmpty(f.Comments) ? "—" : f.Comments)}</td>
                <td>{f.CreatedAt:MMM dd, yyyy}</td>
            </tr>"))}
        </tbody>
    </table>" : "<p>No client reviews received in this period.</p>")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>", "Client Reviews Report");
        }

        public async Task<byte[]> GenerateFreelancerFinancialReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-12);
            endDate ??= DateTime.UtcNow;

            var freelancer = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId);

            var completedBiddings = await _context.Biddings
                .Include(b => b.Project)
                .ThenInclude(p => p.User)
                .Where(b => b.UserId == userId &&
                           b.IsAccepted &&
                           b.Project.Status == "Completed" &&
                           b.BiddingAcceptedDate >= startDate &&
                           b.BiddingAcceptedDate <= endDate)
                .ToListAsync();

            var totalEarnings = completedBiddings.Sum(b => b.Budget);
            var monthlyEarnings = completedBiddings
                .GroupBy(b => b.BiddingAcceptedDate?.ToString("yyyy-MM"))
                .OrderByDescending(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Sum(b => b.Budget));

            var avgProjectValue = completedBiddings.Any()
                ? completedBiddings.Average(b => b.Budget)
                : 0;

            var headerHtml = GenerateReportHeader(
                "Financial Report",
                $"For {freelancer?.FirstName} {freelancer?.LastName}",
                startDate.Value,
                endDate.Value,
                "#059669"
            );

            return await _pdfService.GenerateHtmlToPdfAsync($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Financial Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #059669; }}
        .main-header {{ border-bottom-color: #059669; }}
        .kpi-grid-2 {{ 
            display: grid; 
            grid-template-columns: repeat(2, 1fr); 
            gap: 20px; 
            margin: 25px 0;
        }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>Financial Summary</h2>
    <div class='kpi-grid-2'>
        <div class='kpi'>
            <span class='kpi-value'>PHP {totalEarnings:N0}</span>
            <div class='kpi-label'>Total Earnings</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{completedBiddings.Count}</span>
            <div class='kpi-label'>Completed Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>PHP {avgProjectValue:N0}</span>
            <div class='kpi-label'>Average Project Value</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>PHP {(monthlyEarnings.Any() ? monthlyEarnings.Values.Average() : 0):N0}</span>
            <div class='kpi-label'>Average Monthly Earnings</div>
        </div>
    </div>

    <h2>Monthly Earnings Breakdown</h2>
    {(monthlyEarnings.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Month</th>
                <th class='text-right'>Earnings (PHP)</th>
                <th class='text-right'>Projects Completed</th>
                <th class='text-right'>Average per Project (PHP)</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", monthlyEarnings.Select(me =>
            {
                var monthProjects = completedBiddings.Where(p => p.BiddingAcceptedDate?.ToString("yyyy-MM") == me.Key).ToList();
                var avgPerProject = monthProjects.Any() ? monthProjects.Average(p => p.Budget) : 0;
                return $@"
            <tr>
                <td>{me.Key}</td>
                <td class='text-right'>PHP {me.Value:N0}</td>
                <td class='text-right'>{monthProjects.Count}</td>
                <td class='text-right'>PHP {avgPerProject:N0}</td>
            </tr>";
            }))}
        </tbody>
        <tfoot>
            <tr style='font-weight: bold; background-color: #F3F4F6;'>
                <td>Total</td>
                <td class='text-right'>PHP {totalEarnings:N0}</td>
                <td class='text-right'>{completedBiddings.Count}</td>
                <td class='text-right'>PHP {avgProjectValue:N0}</td>
            </tr>
        </tfoot>
    </table>" : "<p>No earnings data available for this period.</p>")}

    <h2>Project Earnings Details</h2>
    {(completedBiddings.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Project Name</th>
                <th>Client</th>
                <th class='text-right'>Earnings (PHP)</th>
                <th>Completed Date</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", completedBiddings.OrderByDescending(b => b.BiddingAcceptedDate).Select(b => $@"
            <tr>
                <td>{b.Project.ProjectName}</td>
                <td>{b.Project.User.FirstName} {b.Project.User.LastName}</td>
                <td class='text-right'>PHP {b.Budget:N0}</td>
                <td>{b.BiddingAcceptedDate?.ToString("MMM dd, yyyy") ?? "N/A"}</td>
            </tr>"))}
        </tbody>
    </table>" : "<p>No completed projects in this period.</p>")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>", "Financial Report");
        }

        public async Task<byte[]> GenerateClientProjectsCompletionReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-12);
            endDate ??= DateTime.UtcNow;

            var client = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId);

            var projects = await _context.Projects
                .Include(p => p.AcceptedBid)
                .ThenInclude(ab => ab.User)
                .Include(p => p.Biddings)
                .Where(p => p.UserId == userId &&
                           p.CreatedAt >= startDate &&
                           p.CreatedAt <= endDate)
                .ToListAsync();

            var activeProjects = projects.Where(p => p.Status == "Active").ToList();
            var completedProjects = projects.Where(p => p.Status == "Completed").ToList();
            var completionRate = projects.Any() ? (completedProjects.Count * 100.0 / projects.Count) : 0;

            var headerHtml = GenerateReportHeader(
                "Project and Completion Rate Report",
                $"For {client?.FirstName} {client?.LastName}",
                startDate.Value,
                endDate.Value,
                "#1D4ED8"
            );

            return await _pdfService.GenerateHtmlToPdfAsync($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Project and Completion Rate Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #1D4ED8; }}
        .main-header {{ border-bottom-color: #1D4ED8; }}
        .kpi-grid-4 {{ 
            display: grid; 
            grid-template-columns: repeat(4, 1fr); 
            gap: 20px; 
            margin: 25px 0;
        }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>Project Summary</h2>
    <div class='kpi-grid-4'>
        <div class='kpi'>
            <span class='kpi-value'>{projects.Count}</span>
            <div class='kpi-label'>Total Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{activeProjects.Count}</span>
            <div class='kpi-label'>Active Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{completedProjects.Count}</span>
            <div class='kpi-label'>Completed Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{completionRate:F1}%</span>
            <div class='kpi-label'>Completion Rate</div>
        </div>
    </div>

    <h2>Project Details</h2>
    {(projects.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Project Name</th>
                <th>Freelancer</th>
                <th>Budget</th>
                <th>Status</th>
                <th>Created</th>
                <th>Deadline</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", projects.OrderByDescending(p => p.CreatedAt).Select(p => $@"
            <tr>
                <td>{p.ProjectName}</td>
                <td>{(p.AcceptedBid != null ? $"{p.AcceptedBid.User.FirstName} {p.AcceptedBid.User.LastName}" : "—")}</td>
                <td>{(p.AcceptedBid != null ? $"₱{p.AcceptedBid.Budget:N0}" : $"PHP {p.Budget}")}</td>
                <td>{(p.Status ?? "Open")}</td>
                <td>{p.CreatedAt:MMM dd, yyyy}</td>
                <td>{(p.Deadline.HasValue ? p.Deadline.Value.ToString("MMM dd, yyyy") : "—")}</td>
            </tr>"))}
        </tbody>
    </table>" : "<p>No projects created in this period.</p>")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>", "Project and Completion Rate Report");
        }

        public async Task<byte[]> GenerateClientReviewsReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-12);
            endDate ??= DateTime.UtcNow;

            var client = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId);

            var sentReviews = await _context.FreelancerFeedbacks
                .Include(f => f.AcceptBidding)
                .ThenInclude(ab => ab.Project)
                .Include(f => f.Freelancer)
                .Where(f => f.AcceptBidding.Project.UserId == userId &&
                           f.CreatedAt >= startDate &&
                           f.CreatedAt <= endDate)
                .ToListAsync();

            var averageRatingGiven = sentReviews.Any() ? sentReviews.Average(r => r.Rating) : 0;

            var headerHtml = GenerateReportHeader(
                "Reviews Report",
                $"For {client?.FirstName} {client?.LastName}",
                startDate.Value,
                endDate.Value,
                "#1D4ED8"
            );

            return await _pdfService.GenerateHtmlToPdfAsync($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Reviews Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #1D4ED8; }}
        .main-header {{ border-bottom-color: #1D4ED8; }}
        .kpi-grid-2 {{ 
            display: grid; 
            grid-template-columns: repeat(2, 1fr); 
            gap: 20px; 
            margin: 25px 0;
        }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>Project Summary</h2>
    <div class='kpi-grid-2'>
        <div class='kpi'>
            <span class='kpi-value'>{sentReviews.Count}</span>
            <div class='kpi-label'>Reviews Sent</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{averageRatingGiven:F1} ⭐</span>
            <div class='kpi-label'>Average Rating Given</div>
        </div>
    </div>

    <h2>Reviews Sent</h2>
    {(sentReviews.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Project</th>
                <th>Freelancer</th>
                <th>Rating</th>
                <th>Recommend</th>
                <th>Comments</th>
                <th>Date</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", sentReviews.OrderByDescending(r => r.CreatedAt).Select(r => $@"
            <tr>
                <td>{r.AcceptBidding.Project.ProjectName}</td>
                <td>{r.Freelancer.FirstName} {r.Freelancer.LastName}</td>
                <td class='star-rating'>{r.Rating}/5</td>
                <td>{(r.WouldRecommend ? "Yes" : "No")}</td>
                <td class='review-comment'>{(string.IsNullOrEmpty(r.Comments) ? "—" : r.Comments)}</td>
                <td>{r.CreatedAt:MMM dd, yyyy}</td>
            </tr>"))}
        </tbody>
    </table>" : "<p>No reviews given in this period.</p>")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>", "Reviews Report");
        }

        public async Task<byte[]> GenerateClientFinancialReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-12);
            endDate ??= DateTime.UtcNow;

            var client = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId);

            var projects = await _context.Projects
                .Include(p => p.AcceptedBid)
                .ThenInclude(ab => ab.User)
                .Where(p => p.UserId == userId &&
                           p.CreatedAt >= startDate &&
                           p.CreatedAt <= endDate &&
                           p.AcceptedBid != null &&
                           p.Status == "Completed")
                .ToListAsync();

            var totalSpent = projects.Sum(p => p.AcceptedBid.Budget);
            var avgBudget = projects.Any() ? projects.Average(p => p.AcceptedBid.Budget) : 0;

            var headerHtml = GenerateReportHeader(
                "Financial Report",
                $"For {client?.FirstName} {client?.LastName}",
                startDate.Value,
                endDate.Value,
                "#059669"
            );

            return await _pdfService.GenerateHtmlToPdfAsync($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Financial Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #059669; }}
        .main-header {{ border-bottom-color: #059669; }}
        .kpi-grid-2 {{ 
            display: grid; 
            grid-template-columns: repeat(2, 1fr); 
            gap: 20px; 
            margin: 25px 0;
        }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>Project Summary</h2>
    <div class='kpi-grid-2'>
        <div class='kpi'>
            <span class='kpi-value'>PHP {totalSpent:N0}</span>
            <div class='kpi-label'>Total Spent</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>PHP {avgBudget:N0}</span>
            <div class='kpi-label'>Average Budget</div>
        </div>
    </div>

    <h2>Project Financial Details</h2>
    {(projects.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Project Name</th>
                <th>Freelancer</th>
                <th class='text-right'>Amount Spent (PHP)</th>
                <th>Status</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", projects.OrderByDescending(p => p.CreatedAt).Select(p => $@"
            <tr>
                <td>{p.ProjectName}</td>
                <td>{p.AcceptedBid.User.FirstName} {p.AcceptedBid.User.LastName}</td>
                <td class='text-right'>PHP {p.AcceptedBid.Budget:N0}</td>
                <td>{p.Status}</td>
            </tr>"))}
        </tbody>
        <tfoot>
            <tr style='font-weight: bold; background-color: #F3F4F6;'>
                <td colspan='2'>Total</td>
                <td class='text-right'>PHP {totalSpent:N0}</td>
                <td colspan='3'></td>
            </tr>
        </tfoot>
    </table>" : "<p>No completed projects with spending in this period.</p>")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>", "Financial Report");
        }

        public async Task<byte[]> GenerateAdminProjectsOverviewReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-3);
            endDate ??= DateTime.UtcNow;

            var periodLength = (endDate.Value - startDate.Value).Days;
            var previousStartDate = startDate.Value.AddDays(-periodLength);
            var previousEndDate = startDate.Value;

            var totalProjects = await _context.Projects.CountAsync(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate);
            var activeProjects = await _context.Projects.CountAsync(p => p.Status == "Active");
            var completedProjects = await _context.Projects.CountAsync(p => p.Status == "Completed" && p.CreatedAt >= startDate && p.CreatedAt <= endDate);
            var totalBiddings = await _context.Biddings.CountAsync(b => b.Project.CreatedAt >= startDate && b.Project.CreatedAt <= endDate);
            var acceptedBiddings = await _context.Biddings.CountAsync(b => b.IsAccepted && b.Project.CreatedAt >= startDate && b.Project.CreatedAt <= endDate);

            var previousProjects = await _context.Projects.CountAsync(p => p.CreatedAt >= previousStartDate && p.CreatedAt < previousEndDate);
            var previousBiddings = await _context.Biddings.CountAsync(b => b.Project.CreatedAt >= previousStartDate && b.Project.CreatedAt < previousEndDate);
            var previousCompleted = await _context.Projects.CountAsync(p => p.Status == "Completed" && p.CreatedAt >= previousStartDate && p.CreatedAt < previousEndDate);

            var projectGrowth = previousProjects > 0 ? ((totalProjects - previousProjects) * 100.0 / previousProjects) : 0;
            var biddingGrowth = previousBiddings > 0 ? ((totalBiddings - previousBiddings) * 100.0 / previousBiddings) : 0;
            var completionGrowth = previousCompleted > 0 ? ((completedProjects - previousCompleted) * 100.0 / previousCompleted) : 0;
            var bidAcceptanceRate = totalBiddings > 0 ? (acceptedBiddings * 100.0 / totalBiddings) : 0;

            var newProjects = await _context.Projects
                .Include(p => p.User)
                .Include(p => p.AcceptedBid)
                .ThenInclude(ab => ab.User)
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var headerHtml = GenerateReportHeader(
                "Projects Overview and Acceptance Rate Report",
                "",
                startDate.Value,
                endDate.Value,
                "#1D4ED8"
            );

            return await _pdfService.GenerateHtmlToPdfAsync($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Projects Overview and Acceptance Rate Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #1D4ED8; }}
        .main-header {{ border-bottom-color: #1D4ED8; }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>Platform Growth Overview</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{totalProjects}</span>
            <div class='kpi-label'>New Projects</div>
            <span class='trend-indicator {(projectGrowth > 0 ? "trend-up" : projectGrowth < 0 ? "trend-down" : "trend-neutral")}'>
                {(projectGrowth > 0 ? "↑" : projectGrowth < 0 ? "↓" : "→")} {Math.Abs(projectGrowth):F1}% vs previous period
            </span>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{totalBiddings}</span>
            <div class='kpi-label'>Total Bids</div>
            <span class='trend-indicator {(biddingGrowth > 0 ? "trend-up" : biddingGrowth < 0 ? "trend-down" : "trend-neutral")}'>
                {(biddingGrowth > 0 ? "↑" : biddingGrowth < 0 ? "↓" : "→")} {Math.Abs(biddingGrowth):F1}% vs previous period
            </span>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{completedProjects}</span>
            <div class='kpi-label'>Completed Projects</div>
            <span class='trend-indicator {(completionGrowth > 0 ? "trend-up" : completionGrowth < 0 ? "trend-down" : "trend-neutral")}'>
                {(completionGrowth > 0 ? "↑" : completionGrowth < 0 ? "↓" : "→")} {Math.Abs(completionGrowth):F1}% vs previous period
            </span>
        </div>
    </div>

    <h2>Platform Health Metrics</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{activeProjects}</span>
            <div class='kpi-label'>Active Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{bidAcceptanceRate:F1}%</span>
            <div class='kpi-label'>Bid Acceptance Rate</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{acceptedBiddings}</span>
            <div class='kpi-label'>Accepted Biddings</div>
        </div>
    </div>

    <h2>New Projects Details</h2>
    {(newProjects.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Project Name</th>
                <th>Client</th>
                <th>Budget</th>
                <th>Status</th>
                <th>Freelancer</th>
                <th>Created Date</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", newProjects.Select(p => $@"
            <tr>
                <td>{p.ProjectName}</td>
                <td>{p.User.FirstName} {p.User.LastName}</td>
                <td>PHP {(p.AcceptedBid != null ? p.AcceptedBid.Budget : p.Budget):N0}</td>
                <td>{(p.Status ?? "Open")}</td>
                <td>{(p.AcceptedBid != null ? $"{p.AcceptedBid.User.FirstName} {p.AcceptedBid.User.LastName}" : "—")}</td>
                <td>{p.CreatedAt:MMM dd, yyyy}</td>
            </tr>"))}
        </tbody>
    </table>" : "<p>No new projects in this period.</p>")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>", "Projects Overview and Acceptance Rate Report");
        }

        public async Task<byte[]> GenerateAdminProjectDeliveryReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-3);
            endDate ??= DateTime.UtcNow;

            var delayedProjects = await _context.Projects
                .Include(p => p.AcceptedBid)
                .ThenInclude(ab => ab.User)
                .Include(p => p.User)
                .Include(p => p.Contract)
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate &&
                           p.Deadline.HasValue &&
                           ((p.Status == "Active" && DateTime.UtcNow > p.Deadline.Value) ||
                            (p.Status == "Completed" && p.Contract != null &&
                             p.Contract.CompletedAt.HasValue &&
                             p.Contract.CompletedAt.Value > p.Deadline.Value)))
                .ToListAsync();

            var currentlyDelayed = delayedProjects.Where(p => p.Status == "Active").ToList();
            var lateCompleted = delayedProjects.Where(p => p.Status == "Completed").ToList();
            var completedProjects = await _context.Projects.CountAsync(p => p.Status == "Completed" && p.CreatedAt >= startDate && p.CreatedAt <= endDate);
            var onTimeCompleted = completedProjects - lateCompleted.Count;

            var headerHtml = GenerateReportHeader(
                "Project Delivery Performance Report",
                "",
                startDate.Value,
                endDate.Value,
                "#F59E0B"
            );

            return await _pdfService.GenerateHtmlToPdfAsync($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Project Delivery Performance Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #F59E0B; }}
        .main-header {{ border-bottom-color: #F59E0B; }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>Project Delivery Performance</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value' style='color: #EF4444;'>{currentlyDelayed.Count}</span>
            <div class='kpi-label'>Currently Delayed Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value' style='color: #F59E0B;'>{lateCompleted.Count}</span>
            <div class='kpi-label'>Projects Completed Late</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value' style='color: #10B981;'>{onTimeCompleted}</span>
            <div class='kpi-label'>Projects Completed On Time</div>
        </div>
    </div>

    {(currentlyDelayed.Any() ? $@"
    <h2>⚠️ Currently Delayed Projects</h2>
    <table>
        <thead>
            <tr>
                <th>Project Name</th>
                <th>Client</th>
                <th>Freelancer</th>
                <th>Deadline</th>
                <th>Days Overdue</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", currentlyDelayed.OrderBy(p => p.Deadline).Select(p => {
                var daysOverdue = p.Deadline.HasValue ? (DateTime.UtcNow - p.Deadline.Value).Days : 0;
                return $@"
            <tr style='background-color: #FEE2E2;'>
                <td>{p.ProjectName}</td>
                <td>{p.User?.FirstName} {p.User?.LastName}</td>
                <td>{(p.AcceptedBid != null ? $"{p.AcceptedBid.User?.FirstName} {p.AcceptedBid.User?.LastName}" : "—")}</td>
                <td>{p.Deadline?.ToString("MMM dd, yyyy")}</td>
                <td style='color: #DC2626; font-weight: bold;'>{daysOverdue} days</td>
            </tr>";
            }))}
        </tbody>
    </table>" : "<p>No currently delayed projects.</p>")}

    {(lateCompleted.Any() ? $@"
    <h2>Projects Completed Late</h2>
    <table>
        <thead>
            <tr>
                <th>Project Name</th>
                <th>Client</th>
                <th>Freelancer</th>
                <th>Deadline</th>
                <th>Completed Date</th>
                <th>Days Late</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", lateCompleted.OrderByDescending(p => p.Contract?.CompletedAt).Select(p => {
                var daysLate = p.Deadline.HasValue && p.Contract?.CompletedAt.HasValue == true
                    ? (p.Contract.CompletedAt.Value - p.Deadline.Value).Days
                    : 0;
                return $@"
            <tr style='background-color: #FEF3C7;'>
                <td>{p.ProjectName}</td>
                <td>{p.User?.FirstName} {p.User?.LastName}</td>
                <td>{(p.AcceptedBid != null ? $"{p.AcceptedBid.User?.FirstName} {p.AcceptedBid.User?.LastName}" : "—")}</td>
                <td>{p.Deadline?.ToString("MMM dd, yyyy")}</td>
                <td>{p.Contract?.CompletedAt?.ToString("MMM dd, yyyy") ?? "N/A"}</td>
                <td style='color: #D97706; font-weight: bold;'>{daysLate} days</td>
            </tr>";
            }))}
        </tbody>
    </table>" : "<p>No projects completed late in this period.</p>")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>", "Project Delivery Performance Report");
        }

        public async Task<byte[]> GenerateAdminUsersOverviewReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-3);
            endDate ??= DateTime.UtcNow;

            var totalUsers = await _context.UserAccounts.CountAsync();
            var totalFreelancers = await _context.UserAccounts.CountAsync(u => u.Role == "Freelancer" || u.FRole == "Freelancer");
            var totalClients = await _context.UserAccounts.CountAsync(u => u.Role == "Client" || u.FRole == "Client");

            var inactiveFreelancers = await _context.UserAccounts
                .Where(u => (u.Role == "Freelancer" || u.FRole == "Freelancer") &&
                       !_context.Biddings.Any(b => b.UserId == u.Id && b.Project.CreatedAt >= startDate))
                .ToListAsync();

            var lowRatedFreelancers = await _context.FreelancerFeedbacks
                .Where(f => f.CreatedAt >= startDate && f.CreatedAt <= endDate)
                .GroupBy(f => f.FreelancerId)
                .Where(g => g.Average(f => f.Rating) < 3)
                .Select(g => new
                {
                    FreelancerId = g.Key,
                    AverageRating = g.Average(f => f.Rating),
                    ReviewCount = g.Count()
                })
                .ToListAsync();

            var lowRatedUsers = await _context.UserAccounts
                .Where(u => lowRatedFreelancers.Select(l => l.FreelancerId).Contains(u.Id))
                .ToListAsync();

            var headerHtml = GenerateReportHeader(
                "Users Overview Report",
                "",
                startDate.Value,
                endDate.Value,
                "#8B5CF6"
            );

            return await _pdfService.GenerateHtmlToPdfAsync($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Users Overview Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #8B5CF6; }}
        .main-header {{ border-bottom-color: #8B5CF6; }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>User Statistics</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{totalUsers}</span>
            <div class='kpi-label'>Total Users</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{totalFreelancers}</span>
            <div class='kpi-label'>Freelancers</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{totalClients}</span>
            <div class='kpi-label'>Clients</div>
        </div>
    </div>

    <h2>Areas Requiring Attention</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value' style='color: #EF4444;'>{inactiveFreelancers.Count}</span>
            <div class='kpi-label'>Inactive Freelancers</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value' style='color: #F59E0B;'>{lowRatedFreelancers.Count}</span>
            <div class='kpi-label'>Low-Rated Freelancers (&lt;3★)</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{totalFreelancers - inactiveFreelancers.Count}</span>
            <div class='kpi-label'>Active Freelancers</div>
        </div>
    </div>

    {(inactiveFreelancers.Any() ? $@"
    <h2>Inactive Freelancers</h2>
    <table>
        <thead>
            <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Status</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", inactiveFreelancers.Take(50).Select(u => $@"
            <tr>
                <td>{u.FirstName} {u.LastName}</td>
                <td>{u.Email}</td>
                <td style='color: #EF4444; font-weight: bold;'>Inactive</td>
            </tr>"))}
        </tbody>
    </table>" : "<p>No inactive freelancers found.</p>")}

    {(lowRatedUsers.Any() ? $@"
    <h2>Low-Rated Freelancers</h2>
    <table>
        <thead>
            <tr>
                <th>Name</th>
                <th>Email</th>
                <th>Average Rating</th>
                <th>Review Count</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", lowRatedUsers.Select(u => {
                var rating = lowRatedFreelancers.First(l => l.FreelancerId == u.Id);
                return $@"
            <tr style='background-color: #FEF3C7;'>
                <td>{u.FirstName} {u.LastName}</td>
                <td>{u.Email}</td>
                <td class='star-rating' style='color: #EF4444;'>{rating.AverageRating:F1}/5</td>
                <td>{rating.ReviewCount}</td>
            </tr>";
            }))}
        </tbody>
    </table>" : "<p>No low-rated freelancers found.</p>")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>", "Users Overview Report");
        }

        public async Task<byte[]> GenerateAdminFinancialReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-3);
            endDate ??= DateTime.UtcNow;

            var completedProjects = await _context.Projects
                .Include(p => p.AcceptedBid)
                .ThenInclude(ab => ab.User)
                .Include(p => p.User)
                .Where(p => p.Status == "Completed" &&
                           p.AcceptedBid != null &&
                           p.CreatedAt >= startDate &&
                           p.CreatedAt <= endDate)
                .ToListAsync();

            var totalRevenue = completedProjects.Sum(p => p.AcceptedBid.Budget);
            var averageProjectValue = completedProjects.Any() ? completedProjects.Average(p => p.AcceptedBid.Budget) : 0;
            var projectCount = completedProjects.Count;

            var headerHtml = GenerateReportHeader(
                "Financial Overview Report",
                "",
                startDate.Value,
                endDate.Value,
                "#059669"
            );

            return await _pdfService.GenerateHtmlToPdfAsync($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Financial Overview Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #059669; }}
        .main-header {{ border-bottom-color: #059669; }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>Financial Summary</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>₱{totalRevenue:N0}</span>
            <div class='kpi-label'>Total Platform Revenue</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{projectCount}</span>
            <div class='kpi-label'>Completed Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>₱{averageProjectValue:N0}</span>
            <div class='kpi-label'>Average Project Value</div>
        </div>
    </div>

    <h2>Completed Projects Financial Details</h2>
    {(completedProjects.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Project Name</th>
                <th>Client</th>
                <th>Freelancer</th>
                <th class='text-right'>Amount (PHP)</th>
                <th>Completed Date</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", completedProjects.OrderByDescending(p => p.AcceptedBid.Budget).Select(p => $@"
            <tr>
                <td>{p.ProjectName}</td>
                <td>{p.User.FirstName} {p.User.LastName}</td>
                <td>{p.AcceptedBid.User.FirstName} {p.AcceptedBid.User.LastName}</td>
                <td class='text-right'>PHP {p.AcceptedBid.Budget:N0}</td>
                <td>{(p.Contract?.CompletedAt?.ToString("MMM dd, yyyy") ?? p.CreatedAt.ToString("MMM dd, yyyy"))}</td>
            </tr>"))}
        </tbody>
        <tfoot>
            <tr style='font-weight: bold; background-color: #F3F4F6;'>
                <td colspan='3'>Total</td>
                <td class='text-right'>₱{totalRevenue:N0}</td>
                <td></td>
            </tr>
        </tfoot>
    </table>" : "<p>No completed projects in this period.</p>")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>", "Financial Overview Report");
        }

        public async Task<byte[]> GenerateProjectAnalyticsReportAsync(Guid projectId)
        {
            throw new NotImplementedException("Project Analytics Report not yet implemented");
        }

        public async Task<byte[]> GenerateContractReportAsync(Guid contractId)
        {
            throw new NotImplementedException("Contract Report not yet implemented");
        }

        public async Task<byte[]> GenerateMentorshipReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            throw new NotImplementedException("Mentorship Report not yet implemented");
        }


        // Helper methods for platform benchmarks
        private async Task<double> CalculatePlatformSuccessRateAsync(DateTime startDate, DateTime endDate)
        {
            var totalBids = await _context.Biddings
                .Where(b => b.Project.CreatedAt >= startDate && b.Project.CreatedAt <= endDate)
                .CountAsync();

            if (totalBids == 0) return 0;

            var acceptedBids = await _context.Biddings
                .Where(b => b.IsAccepted && b.Project.CreatedAt >= startDate && b.Project.CreatedAt <= endDate)
                .CountAsync();

            return (acceptedBids * 100.0) / totalBids;
        }

        private async Task<double> CalculatePlatformCompletionRateAsync(DateTime startDate, DateTime endDate)
        {
            var totalProjects = await _context.Projects
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
                .CountAsync();

            if (totalProjects == 0) return 0;

            var completedProjects = await _context.Projects
                .Where(p => p.Status == "Completed" && p.CreatedAt >= startDate && p.CreatedAt <= endDate)
                .CountAsync();

            return (completedProjects * 100.0) / totalProjects;
        }

        private string GenerateReportHeader(string reportTitle, string userInfo, DateTime startDate, DateTime endDate, string borderColor = "#3B82F6")
        {
            return $@"
    <div class='report-header'>
        <div class='top-bar'>
            <div class='company-logo-container'>
                <img src='https://ik.imagekit.io/6txj3mofs/GIGHub%20(2).png?updatedAt=1749718355580' 
                    alt='GIGHub Logo' class='company-logo' />
            </div>
            <div class='website-url'>
                <a href='https://www.gighub.com' class='website-link'>www.gighub.com</a>
            </div>
        </div>
        
        <div class='main-header' style='border-bottom-color: {borderColor}'>
            <h1>{reportTitle}</h1>
            <div class='period'>
                {userInfo}<br>
                Period: {startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}
            </div>
        </div>
    </div>";
        }

        private string GetReportStyles()
        {
            return @"
        body { 
            font-family: Inter, Arial, sans-serif; 
            font-size: 14px; 
            color: #111827;
            margin: 0;
            padding: 20px;
            line-height: 1.6;
        }
        
        .report-header {
            margin-bottom: 30px;
        }
        
        .top-bar {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
            padding-bottom: 10px;
        }
        
        .website-url {
            flex-shrink: 0;
        }
        
        .website-link {
            font-size: 12px;
            font-weight: 400;
            color: #000000;
            text-decoration: none;
            padding: 8px 16px;
            transition: all 0.3s ease;
        }
        
        .website-link:hover {
            background: #3B82F6;
            color: white;
        }
        
        .company-logo-container {
            flex: 1;
        }
        
        .company-logo {
            height: 60px;
            width: auto;
            max-width: 200px;
            object-fit: contain;
        }
        
        .main-header {
            text-align: center;
            border-bottom: 3px solid #3B82F6;
            padding-bottom: 20px;
        }
        
        .main-header h1 { 
            margin: 0 0 10px; 
            font-weight: 700; 
            color: #1F2937;
            font-size: 32px;
        }
        
        .period {
            font-size: 14px; 
            color: #6B7280;
            margin-top: 8px;
        }
        
        h2 { 
            margin: 25px 0 15px; 
            font-weight: 600; 
            color: #374151;
            border-bottom: 2px solid #E5E7EB;
            padding-bottom: 8px;
            font-size: 20px;
        }
        
        .kpi-grid { 
            display: grid; 
            grid-template-columns: repeat(3, 1fr); 
            gap: 20px; 
            margin: 25px 0;
        }
        
        .kpi { 
            border: 2px solid #E5E7EB; 
            border-radius: 12px; 
            padding: 20px; 
            background: linear-gradient(135deg, #F9FAFB 0%, #F3F4F6 100%);
            text-align: center;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        
        .kpi-value {
            font-size: 28px;
            font-weight: bold;
            color: #3B82F6;
            margin-bottom: 8px;
            display: block;
        }
        
        .kpi-label {
            font-size: 13px;
            color: #6B7280;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            font-weight: 500;
        }

        .insight-box {
            background: #FEF3C7;
            border-left: 4px solid #F59E0B;
            padding: 20px;
            margin: 20px 0;
            border-radius: 8px;
        }

        .insight-box.success {
            background: #D1FAE5;
            border-left-color: #10B981;
        }

        .insight-box.warning {
            background: #FEE2E2;
            border-left-color: #EF4444;
        }

        .insight-box h3 {
            margin: 0 0 10px;
            color: #92400E;
            font-size: 16px;
        }

        .insight-box.success h3 {
            color: #065F46;
        }

        .insight-box.warning h3 {
            color: #991B1B;
        }

        .insight-box ul {
            margin: 10px 0 0 20px;
            padding: 0;
        }

        .insight-box li {
            margin: 5px 0;
        }

        .recommendation-box {
            background: #EFF6FF;
            border-left: 4px solid #3B82F6;
            padding: 20px;
            margin: 20px 0;
            border-radius: 8px;
        }

        .recommendation-box h3 {
            margin: 0 0 10px;
            color: #1E40AF;
            font-size: 16px;
        }

        .trend-indicator {
            display: inline-block;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 12px;
            font-weight: bold;
            margin-left: 8px;
        }

        .trend-up {
            background: #D1FAE5;
            color: #065F46;
        }

        .trend-down {
            background: #FEE2E2;
            color: #991B1B;
        }

        .trend-neutral {
            background: #F3F4F6;
            color: #6B7280;
        }
        
        table { 
            width: 100%; 
            border-collapse: collapse; 
            margin: 20px 0;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }
        
        th, td { 
            border: 1px solid #E5E7EB; 
            padding: 15px; 
            text-align: left; 
        }
        
        th { 
            background: linear-gradient(135deg, #F3F4F6 0%, #E5E7EB 100%);
            font-weight: 600;
            color: #374151;
            font-size: 14px;
        }
        
        tr:nth-child(even) {
            background-color: #F9FAFB;
        }
        
        tr:hover {
            background-color: #F3F4F6;
        }
        
        .skills-list {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
            margin: 15px 0;
        }
        
        .skill-tag {
            background: #3B82F6;
            color: white;
            padding: 6px 12px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 500;
        }
        
        .review-comment {
            max-width: 300px;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }
        
        .star-rating {
            color: #FBBF24;
            font-weight: bold;
        }
        
        .text-right { 
            text-align: right; 
        }
        
        .footer {
            margin-top: 50px;
            font-size: 12px;
            color: #6B7280;
            text-align: center;
            border-top: 1px solid #E5E7EB;
            padding-top: 20px;
        }";
        }

        private string GenerateFreelancerPerformanceHtml(FreelancerReportData data)
        {
            var acceptedBids = data.Biddings.Where(b => b.IsAccepted).ToList();
            var completedProjects = acceptedBids.Where(b => b.Project.Status == "Completed").ToList();
            var totalEarnings = completedProjects.Sum(b => b.Budget);
            var averageRating = data.Feedbacks.Any() ? data.Feedbacks.Average(f => f.Rating) : 0;
            var successRate = data.Biddings.Any() ? (acceptedBids.Count * 100.0 / data.Biddings.Count) : 0;
            var recommendationRate = data.Feedbacks.Any() ?
                (data.Feedbacks.Count(f => f.WouldRecommend) * 100.0 / data.Feedbacks.Count) : 0;

            var monthlyEarnings = completedProjects
                .GroupBy(b => b.BiddingAcceptedDate?.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Sum(b => b.Budget));

            // Generate specific insights and recommendations
            var insights = GenerateFreelancerInsights(data, successRate, averageRating, recommendationRate, totalEarnings);
            var recommendations = GenerateFreelancerRecommendations(data, successRate, averageRating, completedProjects.Count);

            // Compare with platform averages
            var successRateComparison = successRate >= data.PlatformAverageSuccessRate ? "above" : "below";
            var ratingComparison = averageRating >= data.PlatformAverageRating ? "above" : "below";

            var headerHtml = GenerateReportHeader(
                "Freelancer Performance Report",
                $"For {data.Freelancer?.FirstName} {data.Freelancer?.LastName}",
                data.StartDate,
                data.EndDate,
                "#3B82F6"
            );

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Freelancer Performance Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #3B82F6; }}
        .main-header {{ border-bottom-color: #3B82F6; }}
        .kpi-grid-2 {{ 
            display: grid; 
            grid-template-columns: repeat(2, 1fr); 
            gap: 20px; 
            margin: 25px 0;
        }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>Performance Overview</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{data.Biddings.Count}</span>
            <div class='kpi-label'>Total Bids</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{acceptedBids.Count}</span>
            <div class='kpi-label'>Accepted Bids</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{successRate:F1}%</span>
            <div class='kpi-label'>Success Rate</div>
            <span class='trend-indicator {(successRate >= data.PlatformAverageSuccessRate ? "trend-up" : "trend-down")}'>
                {successRateComparison} platform avg ({data.PlatformAverageSuccessRate:F1}%)
            </span>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.Feedbacks.Count}</span>
            <div class='kpi-label'>Reviews Received</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{averageRating:F1} ⭐</span>
            <div class='kpi-label'>Average Rating</div>
            <span class='trend-indicator {(averageRating >= data.PlatformAverageRating ? "trend-up" : "trend-down")}'>
                {ratingComparison} platform avg ({data.PlatformAverageRating:F1})
            </span>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{recommendationRate:F1}%</span>
            <div class='kpi-label'>Recommendation Rate</div>
        </div>
    </div>

    {insights}

    {recommendations}

    <h2>Financial Summary</h2>
    <div class='kpi-grid-2'>
        <div class='kpi'>
            <span class='kpi-value'>{completedProjects.Count}</span>
            <div class='kpi-label'>Completed Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>₱{totalEarnings:N0}</span>
            <div class='kpi-label'>Total Earnings</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>₱{(completedProjects.Any() ? completedProjects.Average(b => b.Budget) : 0):N0}</span>
            <div class='kpi-label'>Average Project Value</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>₱{(monthlyEarnings.Any() ? monthlyEarnings.Values.Average() : 0):N0}</span>
            <div class='kpi-label'>Average Monthly Earnings</div>
        </div>
    </div>

    <h2>Monthly Earnings Breakdown</h2>
    {(monthlyEarnings.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Month</th>
                <th class='text-right'>Earnings (₱)</th>
                <th class='text-right'>Projects Completed</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", monthlyEarnings.OrderByDescending(me => me.Key).Select(me => $@"
            <tr>
                <td>{me.Key}</td>
                <td class='text-right'>₱{me.Value:N0}</td>
                <td class='text-right'>{completedProjects.Count(p => p.BiddingAcceptedDate?.ToString("yyyy-MM") == me.Key)}</td>
            </tr>"))}
        </tbody>
    </table>" : "<p>No earnings data available for this period.</p>")}

    <h2>Skills Portfolio</h2>
    {(data.Freelancer?.UserAccountSkills?.Any() == true ? $@"
    <div class='skills-list'>
        {string.Join("", data.Freelancer.UserAccountSkills.Select(s => $"<span class='skill-tag'>{s.UserSkill.Name}</span>"))}
    </div>" : "<p>No skills listed in profile.</p>")}

    <h2>Recent Completed Projects</h2>
    {(completedProjects.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Project Name</th>
                <th>Client</th>
                <th class='text-right'>Earnings (₱)</th>
                <th>Completed Date</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", completedProjects.OrderByDescending(b => b.BiddingAcceptedDate).Take(10).Select(p => $@"
            <tr>
                <td>{p.Project.ProjectName}</td>
                <td>{p.Project.User.FirstName} {p.Project.User.LastName}</td>
                <td class='text-right'>₱{p.Budget:N0}</td>
                <td>{(p.BiddingAcceptedDate?.ToString("MMM dd, yyyy") ?? "N/A")}</td>
            </tr>"))}
        </tbody>
    </table>" : "<p>No completed projects in this period.</p>")}

    <h2>Client Reviews Received</h2>
    {(data.Feedbacks.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Project</th>
                <th>Client</th>
                <th>Rating</th>
                <th>Recommend</th>
                <th>Comments</th>
                <th>Date</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", data.Feedbacks.OrderByDescending(f => f.CreatedAt).Take(10).Select(f => $@"
            <tr>
                <td>{f.AcceptBidding.Project.ProjectName}</td>
                <td>{f.AcceptBidding.Project.User.FirstName} {f.AcceptBidding.Project.User.LastName}</td>
                <td class='star-rating'>{new string('⭐', f.Rating)} ({f.Rating}/5)</td>
                <td>{(f.WouldRecommend ? "✅ Yes" : "❌ No")}</td>
                <td class='review-comment'>{(string.IsNullOrEmpty(f.Comments) ? "—" : f.Comments)}</td>
                <td>{f.CreatedAt:MMM dd, yyyy}</td>
            </tr>"))}
        </tbody>
    </table>" : "<p>No client reviews received in this period.</p>")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>";
        }

        private string GenerateFreelancerInsights(FreelancerReportData data, double successRate, double averageRating, double recommendationRate, int totalEarnings)
        {
            var insights = new List<string>();
            var insightType = "insight-box"; // default yellow

            // Success rate analysis
            if (successRate > 40)
            {
                insightType = "insight-box success";
                insights.Add($"Your bid success rate of <strong>{successRate:F1}%</strong> is excellent, indicating strong proposal quality and competitive pricing.");
            }
            else if (successRate > 20)
            {
                insights.Add($"Your bid success rate of <strong>{successRate:F1}%</strong> is moderate. Consider refining your proposals to stand out more.");
            }
            else
            {
                insightType = "insight-box warning";
                insights.Add($"Your bid success rate of <strong>{successRate:F1}%</strong> is below average. Your proposals may need improvement or you might be bidding on projects outside your expertise.");
            }

            // Rating analysis
            if (averageRating >= 4.5)
            {
                insightType = "insight-box success";
                insights.Add($"Outstanding rating of <strong>{averageRating:F1}/5</strong> shows clients are very satisfied with your work quality.");
            }
            else if (averageRating >= 4.0)
            {
                insights.Add($"Good rating of <strong>{averageRating:F1}/5</strong>, but there's room to achieve excellence.");
            }
            else if (averageRating > 0)
            {
                insightType = "insight-box warning";
                insights.Add($"Your rating of <strong>{averageRating:F1}/5</strong> needs improvement. Review negative feedback and address common issues.");
            }

            // Recommendation rate
            if (recommendationRate >= 80)
            {
                insights.Add($"<strong>{recommendationRate:F1}%</strong> of clients would recommend you, showing strong professional reputation.");
            }
            else if (recommendationRate > 0)
            {
                insights.Add($"Only <strong>{recommendationRate:F1}%</strong> would recommend you. Focus on exceeding client expectations.");
            }

            // Earnings insight
            if (totalEarnings > 0)
            {
                var acceptedBids = data.Biddings.Where(b => b.IsAccepted).ToList();
                if (acceptedBids.Any())
                {
                    var avgProjectValue = acceptedBids.Average(b => b.Budget);
                    insights.Add($"Your average project value is <strong>₱{avgProjectValue:N0}</strong>. Consider taking on higher-value projects to increase earnings.");
                }
            }

            if (!insights.Any())
            {
                insights.Add("Insufficient data to generate specific insights. Continue building your portfolio and collecting client feedback.");
            }

            return $@"
    <div class='{insightType}'>
        <h3>📊 Performance Insights</h3>
        <ul>
            {string.Join("", insights.Select(i => $"<li>{i}</li>"))}
        </ul>
    </div>";
        }

        private string GenerateFreelancerRecommendations(FreelancerReportData data, double successRate, double averageRating, int completedProjects)
        {
            var recommendations = new List<string>();

            // Success rate recommendations
            if (successRate < 30)
            {
                recommendations.Add("<strong>Improve Your Proposals:</strong> Personalize each proposal, highlight relevant experience, and clearly explain how you'll solve the client's problem.");
                recommendations.Add("<strong>Bid Strategically:</strong> Focus on projects that match your skills perfectly rather than bidding on everything.");
            }

            // Rating recommendations
            if (averageRating < 4.5 && data.Feedbacks.Any())
            {
                recommendations.Add("<strong>Quality Improvement:</strong> Analyze feedback from lower-rated projects and address recurring issues.");
                recommendations.Add("<strong>Communication:</strong> Maintain regular updates with clients and set clear expectations from the start.");
            }

            // Activity recommendations
            if (data.Biddings.Count < 10)
            {
                recommendations.Add("<strong>Increase Activity:</strong> Submit more bids to increase your chances of winning projects. Aim for at least 15-20 quality bids per month.");
            }

            // Skills recommendations
            if (data.Freelancer?.UserAccountSkills?.Count < 5)
            {
                recommendations.Add("<strong>Expand Skills:</strong> Add more relevant skills to your profile to appear in more searches and attract diverse projects.");
            }

            // Portfolio recommendations
            if (completedProjects < 5)
            {
                recommendations.Add("<strong>Build Portfolio:</strong> Complete more projects to build credibility. Consider taking on smaller projects initially.");
            }
            else if (completedProjects >= 10)
            {
                recommendations.Add("<strong>Showcase Success:</strong> Update your portfolio with your best work and client testimonials to attract higher-paying clients.");
            }

            // Financial growth
            var monthlyEarnings = data.Biddings
                .Where(b => b.IsAccepted && b.Project.Status == "Completed")
                .GroupBy(b => b.BiddingAcceptedDate?.ToString("yyyy-MM"))
                .Select(g => g.Sum(b => b.Budget))
                .ToList();

            if (monthlyEarnings.Count >= 3)
            {
                var trend = monthlyEarnings.Last() - monthlyEarnings.First();
                if (trend < 0)
                {
                    recommendations.Add("<strong>Revenue Declining:</strong> Your monthly earnings are decreasing. Focus on client retention and winning higher-value projects.");
                }
            }

            if (!recommendations.Any())
            {
                recommendations.Add("You're performing well! Keep maintaining your quality standards and continue building client relationships.");
            }

            return $@"
    <div class='recommendation-box'>
        <h3>💡 Actionable Recommendations</h3>
        <ul>
            {string.Join("", recommendations.Select(r => $"<li>{r}</li>"))}
        </ul>
    </div>";
        }

        private string GenerateClientProjectHtml(ClientReportData data)
        {
            var activeProjects = data.Projects.Where(p => p.Status == "Active").ToList();
            var completedProjects = data.Projects.Where(p => p.Status == "Completed").ToList();
            var totalSpent = data.Projects
                .Where(p => p.AcceptedBid != null && p.Status == "Completed")
                .Sum(p => p.AcceptedBid.Budget);
            var avgBudget = data.Projects
                .Where(p => p.AcceptedBid != null && p.Status == "Completed")
                .Any() ?
                data.Projects
                    .Where(p => p.AcceptedBid != null && p.Status == "Completed")
                    .Average(p => p.AcceptedBid.Budget) : 0;

            var averageRatingGiven = data.SentReviews.Any() ? data.SentReviews.Average(r => r.Rating) : 0;
            var recommendationRate = data.SentReviews.Any() ?
                (data.SentReviews.Count(r => r.WouldRecommend) * 100.0 / data.SentReviews.Count) : 0;

            var completionRate = data.Projects.Any() ? (completedProjects.Count * 100.0 / data.Projects.Count) : 0;

            // Generate insights and recommendations
            var insights = GenerateClientInsights(data, completionRate, averageRatingGiven, totalSpent);
            var recommendations = GenerateClientRecommendations(data, completionRate, activeProjects.Count);

            var headerHtml = GenerateReportHeader(
                "Client Project Report",
                $"For {data.Client?.FirstName} {data.Client?.LastName}",
                data.StartDate,
                data.EndDate,
                "#1D4ED8"
            );

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Client Project Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #1D4ED8; }}
        .main-header {{ border-bottom-color: #1D4ED8; }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>Projects Summary</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{data.Projects.Count}</span>
            <div class='kpi-label'>Total Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{activeProjects.Count}</span>
            <div class='kpi-label'>Active Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{completedProjects.Count}</span>
            <div class='kpi-label'>Completed Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{completionRate:F1}%</span>
            <div class='kpi-label'>Completion Rate</div>
            <span class='trend-indicator {(completionRate >= data.PlatformAverageCompletionRate ? "trend-up" : "trend-down")}'>
                {(completionRate >= data.PlatformAverageCompletionRate ? "above" : "below")} platform avg ({data.PlatformAverageCompletionRate:F1}%)
            </span>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>₱{totalSpent:N0}</span>
            <div class='kpi-label'>Total Spent</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>₱{avgBudget:N0}</span>
            <div class='kpi-label'>Average Budget</div>
            <span class='trend-indicator {(avgBudget >= data.PlatformAverageBudget ? "trend-up" : "trend-down")}'>
                {(avgBudget >= data.PlatformAverageBudget ? "above" : "below")} platform avg (₱{data.PlatformAverageBudget:N0})
            </span>
        </div>
    </div>

    {insights}

    {recommendations}

    <h2>Review Statistics</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{data.SentReviews.Count}</span>
            <div class='kpi-label'>Reviews Given</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{averageRatingGiven:F1} ⭐</span>
            <div class='kpi-label'>Average Rating Given</div>
        </div>
    </div>

    <h2>Project Details</h2>
    {(data.Projects.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Project Name</th>
                <th>Freelancer</th>
                <th>Budget</th>
                <th>Status</th>
                <th>Created</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", data.Projects.OrderByDescending(p => p.CreatedAt).Select(p => $@"
            <tr>
                <td>{p.ProjectName}</td>
                <td>{(p.AcceptedBid != null ? $"{p.AcceptedBid.User.FirstName} {p.AcceptedBid.User.LastName}" : "—")}</td>
                <td>{(p.AcceptedBid != null ? $"₱{p.AcceptedBid.Budget:N0}" : $"₱{p.Budget:N0}")}</td>
                <td>{(p.Status ?? "Open")}</td>
                <td>{p.CreatedAt:MMM dd, yyyy}</td>
            </tr>"))}
        </tbody>
    </table>" : "<p>No projects created in this period.</p>")}

    <h2>Reviews Sent</h2>
    {(data.SentReviews.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Project</th>
                <th>Freelancer</th>
                <th>Rating</th>
                <th>Recommend</th>
                <th>Comments</th>
                <th>Date</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", data.SentReviews.OrderByDescending(r => r.CreatedAt).Select(r => $@"
            <tr>
                <td>{r.AcceptBidding.Project.ProjectName}</td>
                <td>{r.Freelancer.FirstName} {r.Freelancer.LastName}</td>
                <td class='star-rating'>{new string('⭐', r.Rating)} ({r.Rating}/5)</td>
                <td>{(r.WouldRecommend ? "✅ Yes" : "❌ No")}</td>
                <td class='review-comment'>{(string.IsNullOrEmpty(r.Comments) ? "—" : r.Comments)}</td>
                <td>{r.CreatedAt:MMM dd, yyyy}</td>
            </tr>"))}
        </tbody>
    </table>" : "<p>No reviews given in this period.</p>")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>";
        }

        private string GenerateClientInsights(ClientReportData data, double completionRate, double averageRatingGiven, int totalSpent)
        {
            var insights = new List<string>();
            var insightType = "insight-box";

            // Completion rate analysis
            if (completionRate >= 80)
            {
                insightType = "insight-box success";
                insights.Add($"Excellent <strong>{completionRate:F1}%</strong> project completion rate shows effective project management and freelancer selection.");
            }
            else if (completionRate >= 60)
            {
                insights.Add($"Your <strong>{completionRate:F1}%</strong> completion rate is good, but there's potential for improvement in project scoping or freelancer vetting.");
            }
            else if (completionRate > 0)
            {
                insightType = "insight-box warning";
                insights.Add($"Your <strong>{completionRate:F1}%</strong> completion rate is below optimal. Review project requirements clarity and freelancer selection criteria.");
            }

            // Budget analysis
            var projectsWithBudget = data.Projects.Where(p => p.AcceptedBid != null).ToList();
            if (projectsWithBudget.Any())
            {
                var avgBudget = projectsWithBudget.Average(p => p.AcceptedBid.Budget);
                if (avgBudget > data.PlatformAverageBudget * 1.5)
                {
                    insights.Add($"Your average project budget is significantly higher than the platform average. You might find better value by exploring more competitive options.");
                }
                else if (avgBudget < data.PlatformAverageBudget * 0.7)
                {
                    insights.Add($"Your average budget is below the platform average. Consider if you're undervaluing projects, which might affect quality.");
                }
            }

            // Review analysis
            if (averageRatingGiven > 0)
            {
                if (averageRatingGiven >= 4.5)
                {
                    insights.Add($"You give high ratings (<strong>{averageRatingGiven:F1}/5 avg</strong>), showing satisfaction with freelancer work. This helps build positive relationships.");
                }
                else if (averageRatingGiven < 3.5)
                {
                    insights.Add($"Your average rating given is <strong>{averageRatingGiven:F1}/5</strong>. Frequent low ratings might indicate issues with freelancer selection or unclear project requirements.");
                }
            }

            // Projects without bids analysis
            var projectsWithoutBids = data.Projects.Where(p => !p.Biddings.Any()).Count();
            if (projectsWithoutBids > data.Projects.Count * 0.3)
            {
                insightType = "insight-box warning";
                insights.Add($"<strong>{projectsWithoutBids}</strong> of your projects received no bids. Consider improving project descriptions, offering competitive budgets, or clarifying requirements.");
            }

            if (!insights.Any())
            {
                insights.Add("Continue posting clear project requirements and maintaining good relationships with freelancers.");
            }

            return $@"
    <div class='{insightType}'>
        <h3>📊 Project Insights</h3>
        <ul>
            {string.Join("", insights.Select(i => $"<li>{i}</li>"))}
        </ul>
    </div>";
        }

        private string GenerateClientRecommendations(ClientReportData data, double completionRate, int activeProjectsCount)
        {
            var recommendations = new List<string>();

            // Completion rate recommendations
            if (completionRate < 70)
            {
                recommendations.Add("<strong>Improve Project Success:</strong> Write detailed project descriptions, set realistic timelines, and verify freelancer portfolios before hiring.");
                recommendations.Add("<strong>Clear Requirements:</strong> Use milestones and deliverables to track progress and ensure alignment.");
            }

            // Active projects management
            if (activeProjectsCount > 5)
            {
                recommendations.Add("<strong>Project Management:</strong> You have many active projects. Consider using project management tools or hiring dedicated freelancers to manage workload effectively.");
            }

            // Review giving
            var unreviewed = data.Projects.Where(p => p.Status == "Completed" && p.AcceptedBid != null)
                .Count(p => !data.SentReviews.Any(r => r.AcceptBidding.ProjectId == p.Id));

            if (unreviewed > 0)
            {
                recommendations.Add($"<strong>Leave Reviews:</strong> You have <strong>{unreviewed}</strong> completed projects without reviews. Feedback helps freelancers improve and guides other clients.");
            }

            // Budget optimization
            var projectsWithBudget = data.Projects.Where(p => p.AcceptedBid != null).ToList();
            if (projectsWithBudget.Any())
            {
                var budgetVariance = projectsWithBudget.Max(p => p.AcceptedBid.Budget) - projectsWithBudget.Min(p => p.AcceptedBid.Budget);
                if (budgetVariance > projectsWithBudget.Average(p => p.AcceptedBid.Budget) * 2)
                {
                    recommendations.Add("<strong>Budget Consistency:</strong> Your project budgets vary significantly. Standardize similar project budgets to attract consistent quality.");
                }
            }

            // Relationship building
            if (data.SentReviews.Any(r => r.Rating >= 4))
            {
                var topFreelancers = data.SentReviews
                    .Where(r => r.Rating >= 4)
                    .GroupBy(r => r.FreelancerId)
                    .Count();

                if (topFreelancers >= 3)
                {
                    recommendations.Add("<strong>Build Long-term Relationships:</strong> You've worked with multiple quality freelancers. Consider rehiring top performers for recurring projects to save time and ensure quality.");
                }
            }

            // Activity recommendations
            if (data.Projects.Count < 3)
            {
                recommendations.Add("<strong>Leverage the Platform:</strong> Post more projects to fully utilize freelancer expertise and grow your business faster.");
            }

            if (!recommendations.Any())
            {
                recommendations.Add("You're managing projects well! Continue providing clear requirements and timely feedback to freelancers.");
            }

            return $@"
    <div class='recommendation-box'>
        <h3>💡 Actionable Recommendations</h3>
        <ul>
            {string.Join("", recommendations.Select(r => $"<li>{r}</li>"))}
        </ul>
    </div>";
        }

        private string GenerateSystemReportHtml(SystemReportData data)
        {
            // Calculate growth trends
            var projectGrowth = data.PreviousPeriodProjects > 0
                ? ((data.TotalProjects - data.PreviousPeriodProjects) * 100.0 / data.PreviousPeriodProjects)
                : 0;
            var biddingGrowth = data.PreviousPeriodBiddings > 0
                ? ((data.TotalBiddings - data.PreviousPeriodBiddings) * 100.0 / data.PreviousPeriodBiddings)
                : 0;
            var completionGrowth = data.PreviousPeriodCompletedProjects > 0
                ? ((data.CompletedProjects - data.PreviousPeriodCompletedProjects) * 100.0 / data.PreviousPeriodCompletedProjects)
                : 0;

            // Generate admin insights
            var insights = GenerateAdminInsights(data, projectGrowth, biddingGrowth);
            var recommendations = GenerateAdminRecommendations(data);

            var headerHtml = GenerateReportHeader(
                "GIGHub System Analytics Report",
                "",
                data.StartDate,
                data.EndDate,
                "#1D4ED8"
            );

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>System Analytics Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #1D4ED8; }}
        .main-header {{ border-bottom-color: #1D4ED8; }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>Platform Growth Overview</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{data.TotalProjects}</span>
            <div class='kpi-label'>New Projects</div>
            <span class='trend-indicator {(projectGrowth > 0 ? "trend-up" : projectGrowth < 0 ? "trend-down" : "trend-neutral")}'>
                {(projectGrowth > 0 ? "↑" : projectGrowth < 0 ? "↓" : "→")} {Math.Abs(projectGrowth):F1}% vs previous period
            </span>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.TotalBiddings}</span>
            <div class='kpi-label'>Total Bids</div>
            <span class='trend-indicator {(biddingGrowth > 0 ? "trend-up" : biddingGrowth < 0 ? "trend-down" : "trend-neutral")}'>
                {(biddingGrowth > 0 ? "↑" : biddingGrowth < 0 ? "↓" : "→")} {Math.Abs(biddingGrowth):F1}% vs previous period
            </span>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.CompletedProjects}</span>
            <div class='kpi-label'>Completed Projects</div>
            <span class='trend-indicator {(completionGrowth > 0 ? "trend-up" : completionGrowth < 0 ? "trend-down" : "trend-neutral")}'>
                {(completionGrowth > 0 ? "↑" : completionGrowth < 0 ? "↓" : "→")} {Math.Abs(completionGrowth):F1}% vs previous period
            </span>
        </div>
    </div>

    <h2>User Statistics</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{data.TotalUsers}</span>
            <div class='kpi-label'>Total Users</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.TotalFreelancers}</span>
            <div class='kpi-label'>Freelancers</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.TotalClients}</span>
            <div class='kpi-label'>Clients</div>
        </div>
    </div>

    {insights}

    <h2>Platform Health Metrics</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{data.ActiveProjects}</span>
            <div class='kpi-label'>Active Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{(data.TotalBiddings > 0 ? (data.AcceptedBiddings * 100.0 / data.TotalBiddings) : 0):F1}%</span>
            <div class='kpi-label'>Bid Acceptance Rate</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.ActiveMentorships}</span>
            <div class='kpi-label'>Active Mentorships</div>
        </div>
    </div>

    <h2>Areas Requiring Attention</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{data.InactiveFreelancers}</span>
            <div class='kpi-label'>Inactive Freelancers</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.ProjectsWithoutBids}</span>
            <div class='kpi-label'>Projects Without Bids</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.LowRatedFreelancers}</span>
            <div class='kpi-label'>Low-Rated Freelancers (&lt;3★)</div>
        </div>
    </div>

    <h2>Project Delivery Performance</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value' style='color: #EF4444;'>{data.DelayedProjects.Count}</span>
            <div class='kpi-label'>Currently Delayed Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value' style='color: #F59E0B;'>{data.LateCompletedProjects.Count}</span>
            <div class='kpi-label'>Projects Completed Late</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value' style='color: #10B981;'>{data.CompletedProjects - data.LateCompletedProjects.Count}</span>
            <div class='kpi-label'>Projects Completed On Time</div>
        </div>
    </div>

    {(data.DelayedProjects.Any() ? $@"
    <h2>⚠️ Currently Delayed Projects</h2>
    <table>
        <thead>
            <tr>
                <th>Project Name</th>
                <th>Client</th>
                <th>Freelancer</th>
                <th>Deadline</th>
                <th>Days Overdue</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", data.DelayedProjects.OrderBy(p => p.Deadline).Select(p => {
                var daysOverdue = p.Deadline.HasValue ? (DateTime.UtcNow - p.Deadline.Value).Days : 0;
                return $@"
            <tr style='background-color: #FEE2E2;'>
                <td>{p.ProjectName}</td>
                <td>{p.User?.FirstName} {p.User?.LastName}</td>
                <td>{(p.AcceptedBid != null ? $"{p.AcceptedBid.User?.FirstName} {p.AcceptedBid.User?.LastName}" : "—")}</td>
                <td>{p.Deadline?.ToString("MMM dd, yyyy")}</td>
                <td style='color: #DC2626; font-weight: bold;'>{daysOverdue} days</td>
            </tr>";
            }))}
        </tbody>
    </table>" : "")}

    {(data.LateCompletedProjects.Any() ? $@"
    <h2>📋 Projects Completed Late</h2>
    <table>
        <thead>
            <tr>
                <th>Project Name</th>
                <th>Client</th>
                <th>Freelancer</th>
                <th>Deadline</th>
                <th>Completed Date</th>
                <th>Days Late</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", data.LateCompletedProjects.OrderByDescending(p => p.Contract?.CompletedAt).Select(p => {
                var daysLate = p.Deadline.HasValue && p.Contract?.CompletedAt.HasValue == true
                    ? (p.Contract.CompletedAt.Value - p.Deadline.Value).Days
                    : 0;
                return $@"
            <tr style='background-color: #FEF3C7;'>
                <td>{p.ProjectName}</td>
                <td>{p.User?.FirstName} {p.User?.LastName}</td>
                <td>{(p.AcceptedBid != null ? $"{p.AcceptedBid.User?.FirstName} {p.AcceptedBid.User?.LastName}" : "—")}</td>
                <td>{p.Deadline?.ToString("MMM dd, yyyy")}</td>
                <td>{p.Contract?.CompletedAt?.ToString("MMM dd, yyyy") ?? "N/A"}</td>
                <td style='color: #D97706; font-weight: bold;'>{daysLate} days</td>
            </tr>";
            }))}
        </tbody>
    </table>" : "")}

    {recommendations}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>";
        }

        private string GenerateAdminInsights(SystemReportData data, double projectGrowth, double biddingGrowth)
        {
            var insights = new List<string>();
            var insightType = "insight-box";

            // Platform growth analysis
            if (projectGrowth > 20)
            {
                insightType = "insight-box success";
                insights.Add($"<strong>Strong Growth:</strong> New projects increased by <strong>{projectGrowth:F1}%</strong>. Platform adoption is accelerating.");
            }
            else if (projectGrowth < -10)
            {
                insightType = "insight-box warning";
                insights.Add($"<strong>Declining Activity:</strong> New projects decreased by <strong>{Math.Abs(projectGrowth):F1}%</strong>. Investigate barriers to client engagement.");
            }

            // Marketplace health
            var bidAcceptanceRate = data.TotalBiddings > 0 ? (data.AcceptedBiddings * 100.0 / data.TotalBiddings) : 0;
            if (bidAcceptanceRate < 10)
            {
                insightType = "insight-box warning";
                insights.Add($"<strong>Low Acceptance Rate:</strong> Only <strong>{bidAcceptanceRate:F1}%</strong> of bids are accepted. This suggests quality issues or mismatch between freelancers and projects.");
            }
            else if (bidAcceptanceRate > 25)
            {
                insightType = "insight-box success";
                insights.Add($"<strong>Healthy Marketplace:</strong> <strong>{bidAcceptanceRate:F1}%</strong> bid acceptance rate indicates good freelancer-project matching.");
            }

            // NEW: Project delivery performance analysis
            var totalProjectsWithDeadlines = data.DelayedProjects.Count + data.LateCompletedProjects.Count + (data.CompletedProjects - data.LateCompletedProjects.Count);
            if (totalProjectsWithDeadlines > 0)
            {
                var onTimeRate = ((data.CompletedProjects - data.LateCompletedProjects.Count) * 100.0 / totalProjectsWithDeadlines);

                if (onTimeRate >= 80)
                {
                    insights.Add($"<strong>Excellent Delivery:</strong> <strong>{onTimeRate:F1}%</strong> of projects are completed on time, showing strong freelancer reliability.");
                }
                else if (onTimeRate < 60)
                {
                    insightType = "insight-box warning";
                    insights.Add($"<strong>Delivery Issues:</strong> Only <strong>{onTimeRate:F1}%</strong> of projects completed on time. Review freelancer accountability and project scoping.");
                }
            }

            if (data.DelayedProjects.Count > 0)
            {
                insightType = "insight-box warning";
                insights.Add($"<strong>Active Delays:</strong> <strong>{data.DelayedProjects.Count}</strong> projects are currently overdue. Immediate intervention may be needed.");
            }

            // User balance
            var freelancerToClientRatio = data.TotalClients > 0 ? (data.TotalFreelancers * 1.0 / data.TotalClients) : 0;
            if (freelancerToClientRatio < 1)
            {
                insights.Add($"<strong>Freelancer Shortage:</strong> Ratio of {freelancerToClientRatio:F2}:1 freelancers to clients. Consider recruiting more freelancers.");
            }
            else if (freelancerToClientRatio > 5)
            {
                insights.Add($"<strong>Client Shortage:</strong> Ratio of {freelancerToClientRatio:F2}:1 freelancers to clients. Focus on client acquisition.");
            }

            // Inactive users
            if (data.InactiveFreelancers > data.TotalFreelancers * 0.3)
            {
                insightType = "insight-box warning";
                insights.Add($"<strong>High Inactivity:</strong> <strong>{data.InactiveFreelancers}</strong> freelancers ({(data.InactiveFreelancers * 100.0 / data.TotalFreelancers):F1}%) are inactive. Implement re-engagement campaigns.");
            }

            // Projects without bids
            if (data.ProjectsWithoutBids > data.TotalProjects * 0.2)
            {
                insightType = "insight-box warning";
                insights.Add($"<strong>Matching Issues:</strong> <strong>{data.ProjectsWithoutBids}</strong> projects received no bids. Review project visibility and matching algorithms.");
            }

            if (!insights.Any())
            {
                insights.Add("Platform metrics are stable. Continue monitoring key indicators.");
            }

            return $@"
    <div class='{insightType}'>
        <h3>📊 Platform Health Insights</h3>
        <ul>
            {string.Join("", insights.Select(i => $"<li>{i}</li>"))}
        </ul>
    </div>";
        }

        private string GenerateAdminRecommendations(SystemReportData data)
        {
            var recommendations = new List<string>();

            // NEW: Delayed project recommendations
            if (data.DelayedProjects.Count > 0)
            {
                recommendations.Add($"<strong>Address Delayed Projects:</strong> {data.DelayedProjects.Count} projects are overdue. Contact freelancers and clients to identify blockers and provide support to get projects back on track.");
            }

            if (data.LateCompletedProjects.Count > data.CompletedProjects * 0.3)
            {
                recommendations.Add($"<strong>Improve Delivery Timeliness:</strong> {((data.LateCompletedProjects.Count * 100.0) / Math.Max(1, data.CompletedProjects)):F1}% of projects completed late. Consider implementing milestone tracking, automated deadline reminders, or freelancer performance penalties.");
            }

            // Inactive freelancer engagement
            if (data.InactiveFreelancers > data.TotalFreelancers * 0.3)
            {
                recommendations.Add($"<strong>Re-engage Inactive Freelancers:</strong> Launch targeted email campaigns, offer incentives for first bids, or provide training resources to activate {data.InactiveFreelancers} inactive freelancers.");
            }

            // Projects without bids
            if (data.ProjectsWithoutBids > 5)
            {
                recommendations.Add($"<strong>Improve Project Visibility:</strong> {data.ProjectsWithoutBids} projects received no bids. Enhance search algorithms, send notifications to relevant freelancers, or guide clients in writing better descriptions.");
            }

            // Low-rated freelancers
            if (data.LowRatedFreelancers > 0)
            {
                recommendations.Add($"<strong>Quality Intervention:</strong> {data.LowRatedFreelancers} freelancers have low ratings. Consider implementing quality improvement programs or stricter vetting.");
            }

            // Bid acceptance rate
            var bidAcceptanceRate = data.TotalBiddings > 0 ? (data.AcceptedBiddings * 100.0 / data.TotalBiddings) : 0;
            if (bidAcceptanceRate < 15)
            {
                recommendations.Add($"<strong>Improve Matching Quality:</strong> Low bid acceptance rate ({bidAcceptanceRate:F1}%) suggests poor freelancer-project matching. Enhance recommendation algorithms or provide bid coaching.");
            }

            // Platform growth
            var projectGrowth = data.PreviousPeriodProjects > 0
                ? ((data.TotalProjects - data.PreviousPeriodProjects) * 100.0 / data.PreviousPeriodProjects)
                : 0;

            if (projectGrowth < 0)
            {
                recommendations.Add($"<strong>Reverse Declining Growth:</strong> Project creation decreased by {Math.Abs(projectGrowth):F1}%. Invest in marketing, improve user experience, or offer promotional campaigns.");
            }
            else if (projectGrowth > 30)
            {
                recommendations.Add($"<strong>Scale Infrastructure:</strong> Strong {projectGrowth:F1}% growth in projects. Ensure platform capacity can handle increased load and consider hiring support staff.");
            }

            // User balance
            var freelancerToClientRatio = data.TotalClients > 0 ? (data.TotalFreelancers * 1.0 / data.TotalClients) : 0;
            if (freelancerToClientRatio < 2)
            {
                recommendations.Add($"<strong>Recruit Freelancers:</strong> Ratio of {freelancerToClientRatio:F2}:1 is imbalanced. Launch freelancer recruitment campaigns or partnerships with educational institutions.");
            }
            else if (freelancerToClientRatio > 8)
            {
                recommendations.Add($"<strong>Client Acquisition Focus:</strong> High freelancer-to-client ratio ({freelancerToClientRatio:F2}:1). Invest in B2B sales, advertising, or client referral programs.");
            }

            // Mentorship program
            var mentorshipEngagement = data.TotalMentorshipMatches > 0
                ? (data.ActiveMentorships * 100.0 / data.TotalMentorshipMatches)
                : 0;

            if (data.TotalMentorshipMatches > 0 && mentorshipEngagement < 50)
            {
                recommendations.Add($"<strong>Boost Mentorship Engagement:</strong> Only {mentorshipEngagement:F1}% of mentorships are active. Provide structured programs, incentives, or better matching to increase engagement.");
            }

            if (!recommendations.Any())
            {
                recommendations.Add("Platform is operating efficiently. Continue monitoring metrics and maintaining quality standards.");
            }

            return $@"
    <div class='recommendation-box'>
        <h3>💡 Strategic Recommendations for Platform Growth</h3>
        <ul>
            {string.Join("", recommendations.Select(r => $"<li>{r}</li>"))}
        </ul>
    </div>";
        }

        private string GenerateFinancialReportHtml(FinancialReportData data)
        {
            var totalEarnings = data.CompletedBiddings.Sum(b => b.Budget);
            var monthlyEarnings = data.CompletedBiddings
                .GroupBy(b => b.BiddingAcceptedDate?.ToString("yyyy-MM"))
                .OrderByDescending(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Sum(b => b.Budget));

            var avgProjectValue = data.CompletedBiddings.Any()
                ? data.CompletedBiddings.Average(b => b.Budget)
                : 0;
            var avgMonthlyEarnings = monthlyEarnings.Any()
                ? monthlyEarnings.Values.Average()
                : 0;

            // Calculate earnings trend
            var earningsTrend = "";
            if (monthlyEarnings.Count >= 2)
            {
                var recentMonths = monthlyEarnings.Take(3).Select(m => m.Value).ToList();
                var isGrowing = recentMonths.Count >= 2 && recentMonths[0] > recentMonths[1];
                earningsTrend = isGrowing
                    ? "<span class='trend-indicator trend-up'>📈 Growing</span>"
                    : "<span class='trend-indicator trend-down'>📉 Declining</span>";
            }

            // Generate insights
            var insights = GenerateFinancialInsights(data, totalEarnings, avgMonthlyEarnings);
            var recommendations = GenerateFinancialRecommendations(data, monthlyEarnings);

            var headerHtml = GenerateReportHeader(
                "Financial Report",
                $"For {data.Freelancer?.FirstName} {data.Freelancer?.LastName}",
                data.StartDate,
                data.EndDate,
                "#059669"  // Green theme for financial reports
            );

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Financial Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #059669; }}
        .main-header {{ border-bottom-color: #059669; }}
        .kpi-grid-2 {{ 
            display: grid; 
            grid-template-columns: repeat(2, 1fr); 
            gap: 20px; 
            margin: 25px 0;
        }}
    </style>
</head>
<body>
    {headerHtml}

    <h2>Financial Overview {earningsTrend}</h2>
    <div class='kpi-grid-2'>
        <div class='kpi'>
            <span class='kpi-value'>₱{totalEarnings:N0}</span>
            <div class='kpi-label'>Total Earnings</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.CompletedBiddings.Count}</span>
            <div class='kpi-label'>Completed Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>₱{avgProjectValue:N0}</span>
            <div class='kpi-label'>Average Project Value</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>₱{avgMonthlyEarnings:N0}</span>
            <div class='kpi-label'>Average Monthly Earnings</div>
        </div>
    </div>

    {insights}

    {recommendations}

    <h2>Monthly Earnings Breakdown</h2>
    {(monthlyEarnings.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Month</th>
                <th class='text-right'>Earnings (₱)</th>
                <th class='text-right'>Projects Completed</th>
                <th class='text-right'>Average per Project (₱)</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", monthlyEarnings.Select(me =>
            {
                var monthProjects = data.CompletedBiddings.Where(p => p.BiddingAcceptedDate?.ToString("yyyy-MM") == me.Key).ToList();
                var avgPerProject = monthProjects.Any() ? monthProjects.Average(p => p.Budget) : 0;
                return $@"
            <tr>
                <td>{me.Key}</td>
                <td class='text-right'>₱{me.Value:N0}</td>
                <td class='text-right'>{monthProjects.Count}</td>
                <td class='text-right'>₱{avgPerProject:N0}</td>
            </tr>";
            }))}
        </tbody>
        <tfoot>
            <tr style='font-weight: bold; background-color: #F3F4F6;'>
                <td>Total</td>
                <td class='text-right'>₱{totalEarnings:N0}</td>
                <td class='text-right'>{data.CompletedBiddings.Count}</td>
                <td class='text-right'>₱{avgProjectValue:N0}</td>
            </tr>
        </tfoot>
    </table>" : "<p>No earnings data available for this period.</p>")}

    <h2>Project Details</h2>
    {(data.CompletedBiddings.Any() ? $@"
    <table>
        <thead>
            <tr>
                <th>Project Name</th>
                <th>Client</th>
                <th class='text-right'>Earnings (₱)</th>
                <th>Completed Date</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", data.CompletedBiddings.OrderByDescending(b => b.BiddingAcceptedDate).Select(b => $@"
            <tr>
                <td>{b.Project.ProjectName}</td>
                <td>{b.Project.User.FirstName} {b.Project.User.LastName}</td>
                <td class='text-right'>₱{b.Budget:N0}</td>
                <td>{b.BiddingAcceptedDate?.ToString("MMM dd, yyyy") ?? "N/A"}</td>
            </tr>"))}
        </tbody>
    </table>" : "<p>No completed projects in this period.</p>")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>";
        }

        private string GenerateFinancialInsights(FinancialReportData data, int totalEarnings, double avgMonthlyEarnings)
        {
            var insights = new List<string>();
            var insightType = "insight-box";

            // Earnings analysis
            if (totalEarnings > 100000)
            {
                insightType = "insight-box success";
                insights.Add($"<strong>Strong Earnings:</strong> Total earnings of <strong>₱{totalEarnings:N0}</strong> demonstrate excellent productivity and value delivery.");
            }
            else if (totalEarnings > 50000)
            {
                insights.Add($"<strong>Solid Performance:</strong> Earned <strong>₱{totalEarnings:N0}</strong> in this period, showing consistent project completion.");
            }
            else if (totalEarnings > 0)
            {
                insights.Add($"<strong>Building Momentum:</strong> Earned <strong>₱{totalEarnings:N0}</strong>. Focus on increasing project volume and rates.");
            }

            // Project value analysis
            if (data.CompletedBiddings.Any())
            {
                var avgProjectValue = data.CompletedBiddings.Average(b => b.Budget);
                if (avgProjectValue > 20000)
                {
                    insightType = "insight-box success";
                    insights.Add($"<strong>High-Value Projects:</strong> Your average project value of <strong>₱{avgProjectValue:N0}</strong> positions you in the premium market segment.");
                }
                else if (avgProjectValue < 5000)
                {
                    insights.Add($"<strong>Growth Opportunity:</strong> Average project value is <strong>₱{avgProjectValue:N0}</strong>. Consider targeting higher-budget projects to increase earnings.");
                }
            }

            // Monthly earnings trend
            var monthlyEarnings = data.CompletedBiddings
                .GroupBy(b => b.BiddingAcceptedDate?.ToString("yyyy-MM"))
                .OrderByDescending(g => g.Key)
                .Take(3)
                .Select(g => g.Sum(b => b.Budget))
                .ToList();

            if (monthlyEarnings.Count >= 2)
            {
                if (monthlyEarnings[0] > monthlyEarnings[1])
                {
                    var growthPercent = ((monthlyEarnings[0] - monthlyEarnings[1]) * 100.0 / monthlyEarnings[1]);
                    insightType = "insight-box success";
                    insights.Add($"<strong>Positive Trend:</strong> Earnings increased by <strong>{growthPercent:F1}%</strong> in the most recent month, showing upward momentum.");
                }
                else if (monthlyEarnings[0] < monthlyEarnings[1])
                {
                    var declinePercent = ((monthlyEarnings[1] - monthlyEarnings[0]) * 100.0 / monthlyEarnings[1]);
                    insightType = "insight-box warning";
                    insights.Add($"<strong>Declining Trend:</strong> Earnings decreased by <strong>{declinePercent:F1}%</strong> in the most recent month. Review your bidding strategy and availability.");
                }
            }

            // Productivity insights
            if (data.CompletedBiddings.Any())
            {
                var projectsPerMonth = data.CompletedBiddings.Count / Math.Max(1,
                    ((data.EndDate - data.StartDate).Days / 30.0));

                if (projectsPerMonth >= 4)
                {
                    insights.Add($"<strong>High Productivity:</strong> Completing approximately <strong>{projectsPerMonth:F1}</strong> projects per month shows excellent time management.");
                }
                else if (projectsPerMonth < 2)
                {
                    insights.Add($"<strong>Capacity Available:</strong> Averaging <strong>{projectsPerMonth:F1}</strong> projects per month. You have capacity to take on more work.");
                }
            }

            if (!insights.Any())
            {
                insights.Add("Start completing projects to see detailed financial insights and track your earnings growth.");
            }

            return $@"
    <div class='{insightType}'>
        <h3>💰 Financial Insights</h3>
        <ul>
            {string.Join("", insights.Select(i => $"<li>{i}</li>"))}
        </ul>
    </div>";
        }

        private string GenerateFinancialRecommendations(FinancialReportData data, Dictionary<string, int> monthlyEarnings)
        {
            var recommendations = new List<string>();

            // Revenue diversification
            if (data.CompletedBiddings.Any())
            {
                var clientDistribution = data.CompletedBiddings
                    .GroupBy(b => b.Project.UserId)
                    .Count();

                if (clientDistribution == 1)
                {
                    recommendations.Add("<strong>Diversify Client Base:</strong> All earnings come from one client. Expand your client portfolio to reduce income risk.");
                }
                else if (clientDistribution >= 5)
                {
                    recommendations.Add("<strong>Strong Diversification:</strong> Working with multiple clients is excellent for income stability. Consider building long-term relationships with top clients.");
                }
            }

            // Project value optimization
            if (data.CompletedBiddings.Any())
            {
                var avgProjectValue = data.CompletedBiddings.Average(b => b.Budget);
                var highValueProjects = data.CompletedBiddings.Where(b => b.Budget > avgProjectValue * 1.5).Count();
                var lowValueProjects = data.CompletedBiddings.Where(b => b.Budget < avgProjectValue * 0.5).Count();

                if (lowValueProjects > highValueProjects)
                {
                    recommendations.Add($"<strong>Raise Your Rates:</strong> You're completing many lower-value projects. Focus on projects worth ₱{(avgProjectValue * 1.5):N0}+ to maximize earnings per hour.");
                }
            }

            // Earnings consistency
            if (monthlyEarnings.Count >= 3)
            {
                var stdDev = CalculateStandardDeviation(monthlyEarnings.Values.Select(v => (double)v).ToList());
                var avgEarnings = monthlyEarnings.Values.Average();

                if (stdDev > avgEarnings * 0.5)
                {
                    recommendations.Add("<strong>Stabilize Income:</strong> Your monthly earnings vary significantly. Build a project pipeline and consider retainer agreements for consistent cash flow.");
                }
            }

            // Volume recommendations
            if (data.CompletedBiddings.Count < 5)
            {
                recommendations.Add("<strong>Increase Project Volume:</strong> Complete more projects to build a stronger financial foundation. Aim for at least 2-3 projects per month.");
            }

            // Growth strategy
            var totalEarnings = data.CompletedBiddings.Sum(b => b.Budget);
            if (totalEarnings < 50000)
            {
                recommendations.Add("<strong>Scale Your Business:</strong> Set a goal to reach ₱50,000+ monthly. Focus on higher-value projects and improving your skill set.");
            }
            else if (totalEarnings > 100000)
            {
                recommendations.Add("<strong>Consider Premium Positioning:</strong> Your earnings show strong performance. Position yourself as a premium freelancer and target enterprise clients.");
            }

            // Financial planning
            if (data.CompletedBiddings.Any())
            {
                recommendations.Add("<strong>Financial Planning:</strong> Set aside 20-30% of earnings for taxes and emergency fund. Consider professional accounting services.");
            }

            if (!recommendations.Any())
            {
                recommendations.Add("Complete more projects to receive personalized financial recommendations for growing your freelance business.");
            }

            return $@"
    <div class='recommendation-box'>
        <h3>💡 Financial Growth Recommendations</h3>
        <ul>
            {string.Join("", recommendations.Select(r => $"<li>{r}</li>"))}
        </ul>
    </div>";
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count < 2) return 0;

            var avg = values.Average();
            var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumOfSquares / values.Count);
        }
    }


    // Enhanced data classes for reports
    public class FreelancerReportData
    {
        public UserAccount Freelancer { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<Bidding> Biddings { get; set; }
        public List<FreelancerFeedback> Feedbacks { get; set; }

        // Platform benchmarks for comparison
        public double PlatformAverageRating { get; set; }
        public double PlatformAverageSuccessRate { get; set; }
    }

    public class ClientReportData
    {
        public UserAccount Client { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<Project> Projects { get; set; }
        public List<FreelancerFeedback> Feedbacks { get; set; }
        public List<FreelancerFeedback> SentReviews { get; set; }

        // Platform benchmarks for comparison
        public double PlatformAverageBudget { get; set; }
        public double PlatformAverageCompletionRate { get; set; }
    }

    public class SystemReportData
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalUsers { get; set; }
        public int TotalFreelancers { get; set; }
        public int TotalClients { get; set; }
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int CompletedProjects { get; set; }
        public int TotalBiddings { get; set; }
        public int AcceptedBiddings { get; set; }
        public int TotalMentorshipMatches { get; set; }
        public int ActiveMentorships { get; set; }

        // Previous period data for trend analysis
        public int PreviousPeriodProjects { get; set; }
        public int PreviousPeriodBiddings { get; set; }
        public int PreviousPeriodCompletedProjects { get; set; }

        // Problem areas
        public int InactiveFreelancers { get; set; }
        public int ProjectsWithoutBids { get; set; }
        public int LowRatedFreelancers { get; set; }

        // NEW: Delayed/Late projects
        public List<Project> DelayedProjects { get; set; } = new List<Project>();
        public List<Project> LateCompletedProjects { get; set; } = new List<Project>();
    }

    public class FinancialReportData
    {
        public UserAccount Freelancer { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<Bidding> CompletedBiddings { get; set; }
    }
}