using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Freelancing.Data;
using Freelancing.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Freelancing.Services
{
    public class UserCleanupHostedService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<UserCleanupHostedService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(24);
        private readonly int _retentionDays;

        public UserCleanupHostedService(IServiceProvider services, ILogger<UserCleanupHostedService> logger, IConfiguration configuration)
        {
            _services = services;
            _logger = logger;
            _retentionDays = configuration.GetValue<int?>("UserCleanup:RetentionDays") ?? 30;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("UserCleanupHostedService started. Running every {Interval}. RetentionDays={RetentionDays}", _interval, _retentionDays);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunCleanup(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in UserCleanupHostedService");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task RunCleanup(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserAccount>>();

            var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);

            var users = await db.UserAccounts
                .Where(u => u.IsDeleted && u.DeletedAt.HasValue && u.DeletedAt.Value <= cutoff)
                .ToListAsync(cancellationToken);

            if (!users.Any())
            {
                _logger.LogInformation("No users found for cleanup.");
                return;
            }

            _logger.LogInformation("Processing {Count} soft-deleted users for cleanup.", users.Count);

            foreach (var user in users)
            {
                try
                {
                    user.Email = $"gighubuser@gighub.local";
                    user.UserName = $"gighubuser";
                    user.NormalizedEmail = user.Email.ToUpperInvariant();
                    user.NormalizedUserName = user.UserName.ToUpperInvariant();
                    user.FirstName = "Gighub";
                    user.LastName = "User";
                    user.Photo = null;
                    user.PhoneNumber = null;
                    user.Bio = null;
                    user.ExperienceLevel = null;
                    user.DeletionReason = "Auto-anonymized after retention period";
                    user.LockoutEnabled = true;
                    user.LockoutEnd = DateTimeOffset.MaxValue;

                    var updateResult = await userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        _logger.LogWarning("Anonymization failed for user {UserId}: {Errors}", user.Id, string.Join("; ", updateResult.Errors.Select(e => e.Description)));
                    }
                    else
                    {
                        _logger.LogInformation("Anonymized user {UserId}", user.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing user {UserId} during cleanup", user.Id);
                }
            }

        }
    }
}