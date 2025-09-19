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
    }
}