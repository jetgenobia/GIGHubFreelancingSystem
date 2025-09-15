using Freelancing.Data;
using Freelancing.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Freelancing.Services
{
    public interface ISmartHiringFeatureService
    {
        Task<MLFeatures> ExtractFeaturesAsync(Guid projectId, string freelancerId);
        Task<List<SmartHiringTrainingData>> PrepareTrainingDataAsync();
        Task ExportTrainingDataToCsvAsync(string filePath);
        Task<int> CalculateMentorshipProgramCompletion(string freelancerId); 
    }

    public class SmartHiringFeatureService : ISmartHiringFeatureService
    {
        private readonly ApplicationDbContext _context;

        public SmartHiringFeatureService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MLFeatures> ExtractFeaturesAsync(Guid projectId, string freelancerId)
        {
            // Get project details
            var project = await _context.Projects
                .Include(p => p.ProjectSkills)
                .ThenInclude(ps => ps.UserSkill)
                .AsSplitQuery() // Split query for better performance
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                throw new ArgumentException("Project not found");

            // Get freelancer details
            var freelancer = await _context.UserAccounts
                .Include(u => u.UserAccountSkills)
                .ThenInclude(uas => uas.UserSkill)
                .AsSplitQuery() // Split query for better performance
                .FirstOrDefaultAsync(u => u.Id == freelancerId);

            if (freelancer == null)
                throw new ArgumentException("Freelancer not found");

            // Calculate features
            var features = new MLFeatures
            {
                SkillMatchScore = await CalculateSkillMatchScore(project, freelancer),
                AvgRating = await CalculateAverageRating(freelancerId),
                RecommendationRate = await CalculateRecommendationRate(freelancerId),
                CompletionRate = await CalculateCompletionRate(freelancerId),
                BidSuccessRate = await CalculateBidSuccessRate(freelancerId),
                CategoryExperience = await CalculateCategoryExperience(freelancerId, project.Category),
                ResponseTimeHours = await CalculateAverageResponseTime(freelancerId),
                PortfolioQuality = await CalculatePortfolioQuality(freelancerId),
                BudgetMatchScore = await CalculateBudgetMatchScore(projectId, freelancerId),
                DeliveryTimeDays = await CalculateProposedDeliveryTime(projectId, freelancerId),
                FreelancerTenureDays = CalculateFreelancerTenure(freelancer),
                ProjectComplexity = CalculateProjectComplexity(project),
                ClientHistoryScore = await CalculateClientHistoryScore(project.UserId, freelancerId),
                PastCollaboration = await HasPastCollaboration(project.UserId, freelancerId) ? 1 : 0,
                SkillsCountMatch = CalculateSkillsCountMatch(project, freelancer),
                WorkloadFactor = await CalculateWorkloadFactor(freelancerId),
                MentorshipProgramCompleted = await CalculateMentorshipProgramCompletion(freelancerId) // Add this line
            };

            return features;
        }

        private async Task<float> CalculateSkillMatchScore(Project project, UserAccount freelancer)
        {
            var requiredSkills = project.ProjectSkills?.Select(ps => ps.UserSkillId).ToList() ?? new List<Guid>();
            var freelancerSkills = freelancer.UserAccountSkills?.Select(uas => uas.UserSkillId).ToList() ?? new List<Guid>();

            if (!requiredSkills.Any())
                return 1.0f; // No specific skills required

            var matchingSkills = requiredSkills.Intersect(freelancerSkills).Count();
            return (float)matchingSkills / requiredSkills.Count;
        }

        private async Task<float> CalculateAverageRating(string freelancerId)
        {
            var ratings = await _context.FreelancerFeedbacks
                .Where(f => f.FreelancerId == freelancerId)
                .Select(f => f.Rating)
                .ToListAsync();

            return ratings.Any() ? (float)ratings.Average() : 3.0f; // Default neutral rating
        }

        private async Task<float> CalculateRecommendationRate(string freelancerId)
        {
            var feedbacks = await _context.FreelancerFeedbacks
                .Where(f => f.FreelancerId == freelancerId)
                .ToListAsync();

            if (!feedbacks.Any())
                return 0.5f; // Default 50% for new freelancers

            var recommendCount = feedbacks.Count(f => f.WouldRecommend);
            return (float)recommendCount / feedbacks.Count;
        }

        private async Task<float> CalculateCompletionRate(string freelancerId)
        {
            var acceptedBids = await _context.Biddings
                .Where(b => b.UserId == freelancerId && b.IsAccepted)
                .Include(b => b.Project)
                .ToListAsync();

            if (!acceptedBids.Any())
                return 0.8f; // Default optimistic rate for new freelancers

            var completedProjects = acceptedBids.Count(b => b.Project.Status == "Completed");
            return (float)completedProjects / acceptedBids.Count;
        }

        private async Task<float> CalculateBidSuccessRate(string freelancerId)
        {
            var totalBids = await _context.Biddings
                .Where(b => b.UserId == freelancerId)
                .CountAsync();

            if (totalBids == 0)
                return 0.1f; // Low default for new freelancers

            var acceptedBids = await _context.Biddings
                .Where(b => b.UserId == freelancerId && b.IsAccepted)
                .CountAsync();

            return (float)acceptedBids / totalBids;
        }

        private async Task<int> CalculateCategoryExperience(string freelancerId, string category)
        {
            return await _context.Biddings
                .Where(b => b.UserId == freelancerId && b.IsAccepted && b.Project.Category == category)
                .CountAsync();
        }

        private async Task<float> CalculateAverageResponseTime(string freelancerId)
        {
            // This would require tracking bid submission times vs project creation times
            // For now, return a default value based on bid history
            var bidCount = await _context.Biddings
                .Where(b => b.UserId == freelancerId)
                .CountAsync();

            // Assume more experienced freelancers respond faster
            return Math.Max(1.0f, 24.0f - (bidCount * 0.5f)); // Hours
        }

        private async Task<float> CalculatePortfolioQuality(string freelancerId)
        {
            var bids = await _context.Biddings
                .Where(b => b.UserId == freelancerId)
                .ToListAsync();

            float score = 0;
            foreach (var bid in bids)
            {
                // Count previous works
                if (!string.IsNullOrEmpty(bid.PreviousWorksPaths))
                {
                    try
                    {
                        var works = JsonSerializer.Deserialize<List<string>>(bid.PreviousWorksPaths);
                        score += works?.Count ?? 0;
                    }
                    catch { }
                }

                // Count repository links
                if (!string.IsNullOrEmpty(bid.RepositoryLinks))
                {
                    try
                    {
                        var repos = JsonSerializer.Deserialize<List<string>>(bid.RepositoryLinks);
                        score += (repos?.Count ?? 0) * 1.5f; // Repositories weighted higher
                    }
                    catch { }
                }
            }

            return Math.Min(score / Math.Max(bids.Count, 1), 10.0f); // Normalize to max 10
        }

        private async Task<float> CalculateBudgetMatchScore(Guid projectId, string freelancerId)
        {
            var project = await _context.Projects.FindAsync(projectId);
            var bid = await _context.Biddings
                .FirstOrDefaultAsync(b => b.ProjectId == projectId && b.UserId == freelancerId);

            if (project == null || bid == null)
                return 0.5f;

            // Parse budget (assuming it's stored as a number or range)
            if (float.TryParse(project.Budget.Replace("$", "").Replace(",", ""), out float projectBudget))
            {
                var bidAmount = bid.Budget;
                var ratio = Math.Min(bidAmount, projectBudget) / Math.Max(bidAmount, projectBudget);
                return ratio;
            }

            return 0.5f; // Default if budget parsing fails
        }

        private async Task<float> CalculateProposedDeliveryTime(Guid projectId, string freelancerId)
        {
            var bid = await _context.Biddings
                .FirstOrDefaultAsync(b => b.ProjectId == projectId && b.UserId == freelancerId);

            if (bid == null)
                return 7.0f; // Default 7 days

            // Parse delivery time from bid.Delivery (assuming it contains time information)
            // This is a simplified implementation - you might need more sophisticated parsing
            var delivery = bid.Delivery.ToLower();
            if (delivery.Contains("day"))
            {
                var numbers = System.Text.RegularExpressions.Regex.Matches(delivery, @"\d+");
                if (numbers.Count > 0 && int.TryParse(numbers[0].Value, out int days))
                {
                    return days;
                }
            }

            return 7.0f; // Default
        }

        private float CalculateFreelancerTenure(UserAccount freelancer)
        {
            // Assuming UserAccount has a creation date field
            // For now, we'll use a simple calculation based on when they might have joined
            // You might need to add a JoinedDate field to UserAccount
            var estimatedJoinDate = DateTime.UtcNow.AddDays(-365); // Default 1 year
            return (float)(DateTime.UtcNow - estimatedJoinDate).TotalDays;
        }

        private float CalculateProjectComplexity(Project project)
        {
            float complexity = 0;

            // Description length
            complexity += Math.Min(project.ProjectDescription.Length / 100.0f, 5.0f);

            // Number of required skills
            complexity += (project.ProjectSkills?.Count ?? 0) * 0.5f;

            // Budget range (higher budget = more complex)
            if (float.TryParse(project.Budget.Replace("$", "").Replace(",", ""), out float budget))
            {
                complexity += Math.Min(budget / 1000.0f, 5.0f);
            }

            return Math.Min(complexity, 10.0f); // Cap at 10
        }

        private async Task<float> CalculateClientHistoryScore(string clientId, string freelancerId)
        {
            var pastProjects = await _context.Projects
                .Where(p => p.UserId == clientId && p.AcceptedBid.UserId == freelancerId)
                .Include(p => p.AcceptedBid)
                .ToListAsync();

            if (!pastProjects.Any())
                return 0.5f; // Neutral for no history

            var successfulProjects = pastProjects.Count(p => p.Status == "Completed");
            return (float)successfulProjects / pastProjects.Count;
        }

        private async Task<bool> HasPastCollaboration(string clientId, string freelancerId)
        {
            return await _context.Projects
                .AnyAsync(p => p.UserId == clientId && p.AcceptedBid.UserId == freelancerId);
        }

        private int CalculateSkillsCountMatch(Project project, UserAccount freelancer)
        {
            var requiredSkills = project.ProjectSkills?.Select(ps => ps.UserSkillId).ToList() ?? new List<Guid>();
            var freelancerSkills = freelancer.UserAccountSkills?.Select(uas => uas.UserSkillId).ToList() ?? new List<Guid>();

            return requiredSkills.Intersect(freelancerSkills).Count();
        }

        private async Task<float> CalculateWorkloadFactor(string freelancerId)
        {
            var activeProjects = await _context.Projects
                .Where(p => p.AcceptedBid.UserId == freelancerId && p.Status == "Active")
                .CountAsync();

            // Normalize workload (0 = available, 1 = very busy)
            return Math.Min(activeProjects / 5.0f, 1.0f);
        }

        public async Task<int> CalculateMentorshipProgramCompletion(string freelancerId)
        {
            // Check if freelancer has completed the peer mentorship program
            var mentorshipRecord = await _context.PeerMentorships
                .FirstOrDefaultAsync(pm => pm.UserId == freelancerId);

            if (mentorshipRecord == null)
                return 0; // Not enrolled in mentorship program

            // Check if they're a mentor (indicates they've likely completed as mentee first)
            if (mentorshipRecord.Role?.ToLower() == "mentor")
                return 1;

            // ✅ FIX: Load data to client first, then check computed property
            var goalCompletions = await _context.MentorshipGoalCompletions
                .Where(mgc => mgc.CompletedByUserId == freelancerId &&
                             mgc.CompletionType.ToLower() == "mentee")
                .ToListAsync(); // ✅ Load to client first

            var hasCompletedGoals = goalCompletions.Any(mgc => mgc.IsFullyCompleted); // ✅ Now use computed property

            if (hasCompletedGoals)
                return 1;

            // Alternative: Check if they have a completed mentorship match
            var hasSuccessfulMatch = await _context.MentorshipMatches
                .AnyAsync(mm => mm.MenteeId == freelancerId && mm.Status.ToLower() == "completed");

            return hasSuccessfulMatch ? 1 : 0;
        }

        public async Task<List<SmartHiringTrainingData>> PrepareTrainingDataAsync()
        {
            var trainingData = new List<SmartHiringTrainingData>();

            // Get all completed projects with accepted bids
            var completedProjects = await _context.Projects
                .Where(p => p.AcceptedBidId.HasValue && p.Status == "Completed")
                .Include(p => p.AcceptedBid)
                .Include(p => p.ProjectSkills)
                .ToListAsync();

            foreach (var project in completedProjects)
            {
                // Get features for the successful match
                var features = await ExtractFeaturesAsync(project.Id, project.AcceptedBid.UserId);
                
                // Determine if it was successful based on feedback
                var feedback = await _context.FreelancerFeedbacks
                    .FirstOrDefaultAsync(f => f.AcceptBidId == project.AcceptedBidId);

                var isSuccessful = feedback?.Rating >= 4 && feedback.WouldRecommend;

                trainingData.Add(new SmartHiringTrainingData
                {
                    SkillMatchScore = features.SkillMatchScore,
                    AvgRating = features.AvgRating,
                    RecommendationRate = features.RecommendationRate,
                    CompletionRate = features.CompletionRate,
                    BidSuccessRate = features.BidSuccessRate,
                    CategoryExperience = features.CategoryExperience,
                    ResponseTimeHours = features.ResponseTimeHours,
                    PortfolioQuality = features.PortfolioQuality,
                    BudgetMatchScore = features.BudgetMatchScore,
                    DeliveryTimeDays = features.DeliveryTimeDays,
                    FreelancerTenureDays = features.FreelancerTenureDays,
                    ProjectComplexity = features.ProjectComplexity,
                    ClientHistoryScore = features.ClientHistoryScore,
                    PastCollaboration = features.PastCollaboration,
                    SkillsCountMatch = features.SkillsCountMatch,
                    WorkloadFactor = features.WorkloadFactor,
                    MentorshipProgramCompleted = features.MentorshipProgramCompleted,
                    IsSuccessfulMatch = isSuccessful ? 1 : 0
                });

                // Also add negative examples (other bidders who weren't selected)
                var otherBidders = await _context.Biddings
                    .Where(b => b.ProjectId == project.Id && !b.IsAccepted)
                    .Take(3) // Limit to avoid data imbalance
                    .ToListAsync();

                foreach (var bidder in otherBidders)
                {
                    var negativeFeatures = await ExtractFeaturesAsync(project.Id, bidder.UserId);

                    trainingData.Add(new SmartHiringTrainingData
                    {
                        SkillMatchScore = negativeFeatures.SkillMatchScore,
                        AvgRating = negativeFeatures.AvgRating,
                        RecommendationRate = negativeFeatures.RecommendationRate,
                        CompletionRate = negativeFeatures.CompletionRate,
                        BidSuccessRate = negativeFeatures.BidSuccessRate,
                        CategoryExperience = negativeFeatures.CategoryExperience,
                        ResponseTimeHours = negativeFeatures.ResponseTimeHours,
                        PortfolioQuality = negativeFeatures.PortfolioQuality,
                        BudgetMatchScore = negativeFeatures.BudgetMatchScore,
                        DeliveryTimeDays = negativeFeatures.DeliveryTimeDays,
                        FreelancerTenureDays = negativeFeatures.FreelancerTenureDays,
                        ProjectComplexity = negativeFeatures.ProjectComplexity,
                        ClientHistoryScore = negativeFeatures.ClientHistoryScore,
                        PastCollaboration = negativeFeatures.PastCollaboration,
                        SkillsCountMatch = negativeFeatures.SkillsCountMatch,
                        WorkloadFactor = negativeFeatures.WorkloadFactor,
                        MentorshipProgramCompleted = negativeFeatures.MentorshipProgramCompleted,
                        IsSuccessfulMatch = 0 // Not selected
                    });
                }
            }

            return trainingData;
        }

        public async Task ExportTrainingDataToCsvAsync(string filePath)
        {
            var trainingData = await PrepareTrainingDataAsync();

            if (trainingData.Count < 50)
            {
                var sampleData = GenerateSampleTrainingData(100);
                trainingData.AddRange(sampleData);
            }

            using var writer = new StreamWriter(filePath);

            // Write header - add mentorship_program_completed
            writer.WriteLine("skill_match_score,avg_rating,recommendation_rate,completion_rate,bid_success_rate," +
                           "category_experience,response_time_hours,portfolio_quality,budget_match_score," +
                           "delivery_time_days,freelancer_tenure_days,project_complexity,client_history_score," +
                           "past_collaboration,skills_count_match,workload_factor,mentorship_program_completed,is_successful_match");

            // Write data - add MentorshipProgramCompleted to the output
            foreach (var row in trainingData)
            {
                writer.WriteLine($"{row.SkillMatchScore:F4},{row.AvgRating:F4},{row.RecommendationRate:F4}," +
                               $"{row.CompletionRate:F4},{row.BidSuccessRate:F4},{row.CategoryExperience}," +
                               $"{row.ResponseTimeHours:F2},{row.PortfolioQuality:F4},{row.BudgetMatchScore:F4}," +
                               $"{row.DeliveryTimeDays:F1},{row.FreelancerTenureDays:F1},{row.ProjectComplexity:F4}," +
                               $"{row.ClientHistoryScore:F4},{row.PastCollaboration},{row.SkillsCountMatch}," +
                               $"{row.WorkloadFactor:F4},{row.MentorshipProgramCompleted},{row.IsSuccessfulMatch}");
            }
        }

        private List<SmartHiringTrainingData> GenerateSampleTrainingData(int count)
        {
            var random = new Random(42);
            var sampleData = new List<SmartHiringTrainingData>();

            for (int i = 0; i < count; i++)
            {
                var skillMatchScore = (float)random.NextDouble();
                var avgRating = 3.0f + (float)random.NextDouble() * 2.0f;
                var hasExperience = random.NextDouble() > 0.3;
                var mentorshipCompleted = random.NextDouble() > 0.7 ? 1 : 0; // 30% completion rate

                // Adjust success probability to account for mentorship completion
                var baseSuccessProbability = (skillMatchScore * 0.35f + (avgRating - 3.0f) / 2.0f * 0.35f + mentorshipCompleted * 0.1f);

                sampleData.Add(new SmartHiringTrainingData
                {
                    SkillMatchScore = skillMatchScore,
                    AvgRating = avgRating,
                    RecommendationRate = hasExperience ? 0.7f + (float)random.NextDouble() * 0.3f : (float)random.NextDouble() * 0.7f,
                    CompletionRate = hasExperience ? 0.8f + (float)random.NextDouble() * 0.2f : 0.6f + (float)random.NextDouble() * 0.4f,
                    BidSuccessRate = 0.1f + (float)random.NextDouble() * 0.4f,
                    CategoryExperience = hasExperience ? random.Next(1, 15) : random.Next(0, 3),
                    ResponseTimeHours = 2f + (float)random.NextDouble() * 48f,
                    PortfolioQuality = (float)random.NextDouble() * 10f,
                    BudgetMatchScore = 0.7f + (float)random.NextDouble() * 0.3f,
                    DeliveryTimeDays = 3f + (float)random.NextDouble() * 25f,
                    FreelancerTenureDays = random.Next(30, 1000),
                    ProjectComplexity = (float)random.NextDouble() * 10f,
                    ClientHistoryScore = random.NextDouble() > 0.8 ? (float)random.NextDouble() : 0.5f,
                    PastCollaboration = random.NextDouble() > 0.85 ? 1 : 0,
                    SkillsCountMatch = (int)(skillMatchScore * 5),
                    WorkloadFactor = (float)random.NextDouble(),
                    MentorshipProgramCompleted = mentorshipCompleted, // Add this line
                    IsSuccessfulMatch = random.NextDouble() < baseSuccessProbability + 0.2 ? 1 : 0
                });
            }

            return sampleData;
        }
    }

    // Data models for ML features
    public class MLFeatures
    {
        public float SkillMatchScore { get; set; }
        public float AvgRating { get; set; }
        public float RecommendationRate { get; set; }
        public float CompletionRate { get; set; }
        public float BidSuccessRate { get; set; }
        public int CategoryExperience { get; set; }
        public float ResponseTimeHours { get; set; }
        public float PortfolioQuality { get; set; }
        public float BudgetMatchScore { get; set; }
        public float DeliveryTimeDays { get; set; }
        public float FreelancerTenureDays { get; set; }
        public float ProjectComplexity { get; set; }
        public float ClientHistoryScore { get; set; }
        public int PastCollaboration { get; set; }
        public int SkillsCountMatch { get; set; }
        public float WorkloadFactor { get; set; }
        public int MentorshipProgramCompleted { get; set; }
    }

    public class SmartHiringTrainingData : MLFeatures
    {
        public int IsSuccessfulMatch { get; set; }
    }
}

