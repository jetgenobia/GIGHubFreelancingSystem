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

            var reportData = new FreelancerReportData
            {
                Freelancer = freelancer,
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                Biddings = biddings,
                Feedbacks = feedbacks
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

            // Updated: Get sent reviews (feedbacks given by this client)
            var sentReviews = await _context.FreelancerFeedbacks
                .Include(f => f.AcceptBidding)
                .ThenInclude(ab => ab.Project)
                .Include(f => f.Freelancer)
                .Where(f => f.AcceptBidding.Project.UserId == userId &&
                           f.CreatedAt >= startDate &&
                           f.CreatedAt <= endDate)
                .ToListAsync();

            var reportData = new ClientReportData
            {
                Client = client,
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                Projects = projects,
                SentReviews = sentReviews // Updated property name for clarity
            };

            var htmlContent = GenerateClientProjectHtml(reportData);
            return await _pdfService.GenerateHtmlToPdfAsync(htmlContent, "Client Project Report");
        }

        public async Task<byte[]> GenerateAdminSystemReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            startDate ??= DateTime.UtcNow.AddMonths(-3);
            endDate ??= DateTime.UtcNow;

            var reportData = new SystemReportData
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                TotalUsers = await _context.UserAccounts.CountAsync(),
                TotalFreelancers = await _context.UserAccounts.CountAsync(u => u.Role == "Freelancer" || u.FRole == "Freelancer"),
                TotalClients = await _context.UserAccounts.CountAsync(u => u.Role == "Client" || u.FRole == "Client"),
                TotalProjects = await _context.Projects.CountAsync(),
                ActiveProjects = await _context.Projects.CountAsync(p => p.Status == "Active"),
                CompletedProjects = await _context.Projects.CountAsync(p => p.Status == "Completed"),
                TotalBiddings = await _context.Biddings.CountAsync(),
                AcceptedBiddings = await _context.Biddings.CountAsync(b => b.IsAccepted),
                TotalMentorshipMatches = await _context.MentorshipMatches.CountAsync(),
                ActiveMentorships = await _context.MentorshipMatches.CountAsync(m => m.Status == "Active")
            };

            var htmlContent = GenerateSystemReportHtml(reportData);
            return await _pdfService.GenerateHtmlToPdfAsync(htmlContent, "System Analytics Report");
        }

        // Add the missing method
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

        // Add other missing methods with basic implementations
        public async Task<byte[]> GenerateProjectAnalyticsReportAsync(Guid projectId)
        {
            // Implementation for project analytics
            throw new NotImplementedException("Project Analytics Report not yet implemented");
        }

        public async Task<byte[]> GenerateContractReportAsync(Guid contractId)
        {
            // Implementation for contract report
            throw new NotImplementedException("Contract Report not yet implemented");
        }

        public async Task<byte[]> GenerateMentorshipReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null)
        {
            // Implementation for mentorship report
            throw new NotImplementedException("Mentorship Report not yet implemented");
        }

        private string GenerateReportHeader(string reportTitle, string userInfo, DateTime startDate, DateTime endDate, string borderColor = "#3B82F6")
        {
            return $@"
    <!-- Report Header with Logo and Website -->
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

            // Add financial calculations from the financial report
            var monthlyEarnings = completedProjects
                .GroupBy(b => b.BiddingAcceptedDate?.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Sum(b => b.Budget));

            var headerHtml = GenerateReportHeader(
                "Freelancer Performance Report",
                $"For {data.Freelancer?.FirstName} {data.Freelancer?.LastName}",
                data.StartDate,
                data.EndDate,
                "#3B82F6"  // Use blue theme for freelancer reports
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
        
        /* Additional style for 2-column KPI grid */
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
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.Feedbacks.Count}</span>
            <div class='kpi-label'>Reviews Received</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{averageRating:F1}</span>
            <div class='kpi-label'>Average Rating</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{recommendationRate:F1}%</span>
            <div class='kpi-label'>Recommendation Rate</div>
        </div>
    </div>

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
    </table>" : "")}

    <h2>Skills Portfolio</h2>
    {(data.Freelancer?.UserAccountSkills?.Any() == true ? $@"
    <div class='skills-list'>
        {string.Join("", data.Freelancer.UserAccountSkills.Select(s => $"<span class='skill-tag'>{s.UserSkill.Name}</span>"))}
    </div>" : "")}

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
    </table>" : "")}

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
    </table>" : "")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>";
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
            <span class='kpi-value'>₱{totalSpent:N0}</span>
            <div class='kpi-label'>Total Spent</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>₱{avgBudget:N0}</span>
            <div class='kpi-label'>Average Budget</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.SentReviews.Count}</span>
            <div class='kpi-label'>Reviews Given</div>
        </div>
    </div>

    <h2>Review Statistics</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{averageRatingGiven:F1} ⭐</span>
            <div class='kpi-label'>Average Rating Given</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{recommendationRate:F1}%</span>
            <div class='kpi-label'>Recommendation Rate</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.SentReviews.Count(r => r.Comments != null && r.Comments.Trim().Length > 0)}</span>
            <div class='kpi-label'>Reviews with Comments</div>
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
                <td>{p.CreatedAt:MMM yyyy}</td>
            </tr>"))}
        </tbody>
    </table>" : "")}

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
    </table>" : "")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>";
        }

        private string GenerateSystemReportHtml(SystemReportData data)
        {
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

    <h2>Project Statistics</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{data.TotalProjects}</span>
            <div class='kpi-label'>Total Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.ActiveProjects}</span>
            <div class='kpi-label'>Active Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.CompletedProjects}</span>
            <div class='kpi-label'>Completed Projects</div>
        </div>
    </div>

    <h2>Platform Activity</h2>
    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>{data.TotalBiddings}</span>
            <div class='kpi-label'>Total Bids</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.AcceptedBiddings}</span>
            <div class='kpi-label'>Accepted Bids</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.TotalMentorshipMatches}</span>
            <div class='kpi-label'>Mentorships</div>
        </div>
    </div>

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>";
        }

        private string GenerateFinancialReportHtml(FinancialReportData data)
        {
            var totalEarnings = data.CompletedBiddings.Sum(b => b.Budget);
            var monthlyEarnings = data.CompletedBiddings
                .GroupBy(b => b.BiddingAcceptedDate?.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Sum(b => b.Budget));
            var headerHtml = GenerateReportHeader(
    "Financial Report",
    $"For {data.Freelancer?.FirstName} {data.Freelancer?.LastName}",
    data.StartDate,
    data.EndDate,
    "#1D4ED8"
);

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Financial Report</title>
    <style>
        {GetReportStyles()}
        .kpi-value {{ color: #1D4ED8; }}
        .main-header {{ border-bottom-color: #1D4ED8; }}
    </style>
</head>
<body>
    {headerHtml}

    <div class='kpi-grid'>
        <div class='kpi'>
            <span class='kpi-value'>₱{totalEarnings:N0}</span>
            <div class='kpi-label'>Total Earnings</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>{data.CompletedBiddings.Count}</span>
            <div class='kpi-label'>Completed Projects</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>₱{(data.CompletedBiddings.Any() ? data.CompletedBiddings.Average(b => b.Budget) : 0):N0}</span>
            <div class='kpi-label'>Average Project Value</div>
        </div>
        <div class='kpi'>
            <span class='kpi-value'>₱{(monthlyEarnings.Any() ? monthlyEarnings.Values.Average() : 0):N0}</span>
            <div class='kpi-label'>Average Monthly Earnings</div>
        </div>
    </div>

    {(monthlyEarnings.Any() ? $@"
    <h2>Monthly Earnings Breakdown</h2>
    <table>
        <thead>
            <tr>
                <th>Month</th>
                <th class='text-right'>Earnings (₱)</th>
            </tr>
        </thead>
        <tbody>
            {string.Join("", monthlyEarnings.Select(me => $@"
            <tr>
                <td>{me.Key}</td>
                <td class='text-right'>{me.Value:N0}</td>
            </tr>"))}
        </tbody>
    </table>" : "")}

    {(data.CompletedBiddings.Any() ? $@"
    <h2>Project Details</h2>
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
                <td class='text-right'>{b.Budget:N0}</td>
                <td>{b.BiddingAcceptedDate?.ToString("MMM dd, yyyy") ?? "N/A"}</td>
            </tr>"))}
        </tbody>
    </table>" : "")}

    <div class='footer'>
        Generated on {DateTime.Now:MMMM dd, yyyy 'at' h:mm tt}
    </div>
</body>
</html>";
        }
    }

    // Data classes for reports
    public class FreelancerReportData
    {
        public UserAccount Freelancer { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<Bidding> Biddings { get; set; }
        public List<FreelancerFeedback> Feedbacks { get; set; }
    }

    public class ClientReportData
    {
        public UserAccount Client { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<Project> Projects { get; set; }
        public List<FreelancerFeedback> Feedbacks { get; set; }
        public List<FreelancerFeedback> SentReviews { get; set; }
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
    }

    public class FinancialReportData
    {
        public UserAccount Freelancer { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<Bidding> CompletedBiddings { get; set; }
    }
}