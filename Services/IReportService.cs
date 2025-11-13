using Freelancing.Models.Entities;
using Freelancing.Models;

namespace Freelancing.Services
{
    public interface IReportService
    {
        Task<byte[]> GenerateFreelancerPerformanceReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateClientProjectReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateFinancialReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateProjectAnalyticsReportAsync(Guid projectId);
        Task<byte[]> GenerateAdminSystemReportAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateContractReportAsync(Guid contractId);
        Task<byte[]> GenerateMentorshipReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);

        // Freelancer specific reports
        Task<byte[]> GenerateFreelancerBidsReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateFreelancerReviewsReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateFreelancerFinancialReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);

        // Client specific reports
        Task<byte[]> GenerateClientProjectsCompletionReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateClientReviewsReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateClientFinancialReportAsync(string userId, DateTime? startDate = null, DateTime? endDate = null);

        Task<byte[]> GenerateAdminProjectsOverviewReportAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateAdminProjectDeliveryReportAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateAdminUsersOverviewReportAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<byte[]> GenerateAdminFinancialReportAsync(DateTime? startDate = null, DateTime? endDate = null);
    }

}