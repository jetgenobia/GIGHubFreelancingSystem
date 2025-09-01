using Freelancing.Models.Entities;
using Freelancing.Services;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Freelancing.Data
{
    public class ApplicationDbContext : IdentityDbContext<UserAccount>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }

        public DbSet<Project> Projects { get; set; }
        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<Bidding> Biddings { get; set; }
        public DbSet<PeerMentorship> PeerMentorships { get; set; }
        public DbSet<UserSkill> UserSkills { get; set; }
        public DbSet<UserAccountSkill> UserAccountSkills { get; set; }
        public DbSet<ProjectSkill> ProjectSkills { get; set; }
        public DbSet<MentorshipMatch> MentorshipMatches { get; set; }
        public DbSet<MentorshipSession> MentorshipSessions { get; set; }
        public DbSet<Goal> Goals { get; set; }
        public DbSet<MentorshipGoalCompletion> MentorshipGoalCompletions { get; set; }
        public DbSet<MentorReview> MentorReviews { get; set; }

        // New chat-related entities
        public DbSet<MentorshipChatMessage> MentorshipChatMessages { get; set; }
        public DbSet<MentorshipChatFile> MentorshipChatFiles { get; set; }
        
        // Chat entities
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<ChatRoom> ChatRooms { get; set; }
        public DbSet<ChatFile> ChatFiles { get; set; }

        // Notification entity
        public DbSet<Notification> Notifications { get; set; }
        
        // Contract entities
        public DbSet<Contract> Contracts { get; set; }
        public DbSet<ContractAuditLog> ContractAuditLogs { get; set; }
        public DbSet<ContractRevision> ContractRevisions { get; set; }
        public DbSet<ContractTemplate> ContractTemplates { get; set; }
        
        // Contract Termination entities
        public DbSet<ContractTermination> ContractTerminations { get; set; }
        public DbSet<ContractTerminationAuditLog> ContractTerminationAuditLogs { get; set; }
        
        // Deliverable entity
        public DbSet<Deliverable> Deliverables { get; set; }
        
        // FreelancerFeedback entity
        public DbSet<FreelancerFeedback> FreelancerFeedbacks { get; set; }
        
        // Smart Hiring entities
        public DbSet<HiringOutcome> HiringOutcomes { get; set; }
        
        // Identity Verification entities
        public DbSet<IdentityVerification> IdentityVerifications { get; set; }
        
        // Portfolio entity
        public DbSet<Portfolio> Portfolios { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Project>()
                .HasOne(p => p.User)
                .WithMany(u => u.Projects)
                .HasForeignKey(p => p.UserId);

            modelBuilder.Entity<Bidding>()
                .HasOne(b => b.User)
                .WithMany(u => u.Biddings)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Bidding>()
                .HasOne(b => b.Project)
                .WithMany(p => p.Biddings)
                .HasForeignKey(b => b.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Bidding>()
                .HasIndex(b => new { b.UserId, b.ProjectId })
                .IsUnique();

            modelBuilder.Entity<Project>()
                .HasOne(p => p.User)
                .WithMany(u => u.Projects)
                .HasForeignKey(p => p.UserId);

            modelBuilder.Entity<Project>()
                .HasOne(p => p.AcceptedBid)
                .WithMany()
                .HasForeignKey(p => p.AcceptedBidId)
                .OnDelete(DeleteBehavior.NoAction);

            // ProjectSkill relationships
            modelBuilder.Entity<ProjectSkill>()
                .HasOne(ps => ps.Project)
                .WithMany(p => p.ProjectSkills)
                .HasForeignKey(ps => ps.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectSkill>()
                .HasOne(ps => ps.UserSkill)
                .WithMany()
                .HasForeignKey(ps => ps.UserSkillId)
                .OnDelete(DeleteBehavior.NoAction);

            // Ensure unique project-skill combinations
            modelBuilder.Entity<ProjectSkill>()
                .HasIndex(ps => new { ps.ProjectId, ps.UserSkillId })
                .IsUnique();

            modelBuilder.Entity<PeerMentorship>()
                .HasOne(u => u.User)
                .WithOne(p => p.Mentorship)
                .HasForeignKey<PeerMentorship>(u => u.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserAccountSkill>()
                .HasKey(uas => new { uas.UserAccountId, uas.UserSkillId });

            modelBuilder.Entity<UserAccountSkill>()
                .HasOne(uas => uas.UserAccount)
                .WithMany(ua => ua.UserAccountSkills)
                .HasForeignKey(uas => uas.UserAccountId);

            modelBuilder.Entity<UserAccountSkill>()
                .HasOne(uas => uas.UserSkill)
                .WithMany()
                .HasForeignKey(uas => uas.UserSkillId);

            // New MentorshipMatch relationships
            modelBuilder.Entity<MentorshipMatch>()
                .HasOne(mm => mm.Mentor)
                .WithMany()
                .HasForeignKey(mm => mm.MentorId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<MentorshipMatch>()
                .HasOne(mm => mm.Mentee)
                .WithMany()
                .HasForeignKey(mm => mm.MenteeId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<MentorshipMatch>()
                .HasOne(mm => mm.MentorMentorship)
                .WithMany()
                .HasForeignKey(mm => mm.MentorMentorshipId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<MentorshipMatch>()
                .HasOne(mm => mm.MenteeMentorship)
                .WithMany()
                .HasForeignKey(mm => mm.MenteeMentorshipId)
                .OnDelete(DeleteBehavior.NoAction);

            // Ensure unique mentor-mentee pairs
            modelBuilder.Entity<MentorshipMatch>()
                .HasIndex(mm => new { mm.MentorId, mm.MenteeId })
                .IsUnique();

            // Add indexes for better performance
            modelBuilder.Entity<MentorshipMatch>()
                .HasIndex(mm => mm.Status);

            modelBuilder.Entity<MentorshipMatch>()
                .HasIndex(mm => mm.MatchedDate);


            // MentorshipChatMessage relationships and configurations
            modelBuilder.Entity<MentorshipChatMessage>()
                .HasOne(mcm => mcm.MentorshipMatch)
                .WithMany()
                .HasForeignKey(mcm => mcm.MentorshipMatchId)
                .OnDelete(DeleteBehavior.Cascade);

            // MentorshipSession relationships and indexes
            modelBuilder.Entity<MentorshipSession>()
                .HasOne(ms => ms.MentorshipMatch)
                .WithMany()
                .HasForeignKey(ms => ms.MentorshipMatchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MentorshipSession>()
                .HasIndex(ms => ms.MentorshipMatchId);

            modelBuilder.Entity<MentorshipChatMessage>()
                .HasOne(mcm => mcm.Sender)
                .WithMany()
                .HasForeignKey(mcm => mcm.SenderId)
                .OnDelete(DeleteBehavior.NoAction);

            // Add indexes for chat messages
            modelBuilder.Entity<MentorshipChatMessage>()
                .HasIndex(mcm => mcm.MentorshipMatchId);

            modelBuilder.Entity<MentorshipChatMessage>()
                .HasIndex(mcm => mcm.SenderId);

            modelBuilder.Entity<MentorshipChatMessage>()
                .HasIndex(mcm => mcm.SentAt);

            modelBuilder.Entity<MentorshipChatMessage>()
                .HasIndex(mcm => new { mcm.MentorshipMatchId, mcm.SentAt });

            // MentorshipChatFile relationships
            modelBuilder.Entity<MentorshipChatFile>()
                .HasOne(mcf => mcf.Message)
                .WithMany()
                .HasForeignKey(mcf => mcf.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure default values
            modelBuilder.Entity<MentorshipChatMessage>()
                .Property(mcm => mcm.SentAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<MentorshipChatMessage>()
                .Property(mcm => mcm.IsRead)
                .HasDefaultValue(false);

            modelBuilder.Entity<MentorshipChatMessage>()
                .Property(mcm => mcm.IsDeleted)
                .HasDefaultValue(false);

            modelBuilder.Entity<MentorshipChatFile>()
                .Property(mcf => mcf.UploadedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // MentorshipGoalCompletion relationships and configurations
            modelBuilder.Entity<MentorshipGoalCompletion>()
                .HasOne(mgc => mgc.MentorshipMatch)
                .WithMany()
                .HasForeignKey(mgc => mgc.MentorshipMatchId)
                .OnDelete(DeleteBehavior.Cascade);

            // Notification relationships and configurations
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.UserId);

            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.CreatedAt);

            modelBuilder.Entity<Notification>()
                .HasIndex(n => n.IsRead);

            modelBuilder.Entity<Notification>()
                .Property(n => n.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Notification>()
                .Property(n => n.IsRead)
                .HasDefaultValue(false);

            modelBuilder.Entity<MentorshipGoalCompletion>()
                .HasOne(mgc => mgc.Goal)
                .WithMany()
                .HasForeignKey(mgc => mgc.GoalId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<MentorshipGoalCompletion>()
                .HasOne(mgc => mgc.CompletedByUser)
                .WithMany()
                .HasForeignKey(mgc => mgc.CompletedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            // Add indexes for better performance
            modelBuilder.Entity<MentorshipGoalCompletion>()
                .HasIndex(mgc => new { mgc.MentorshipMatchId, mgc.GoalId });

            modelBuilder.Entity<MentorshipGoalCompletion>()
                .HasIndex(mgc => mgc.CompletedByUserId);

            modelBuilder.Entity<MentorshipGoalCompletion>()
                .HasIndex(mgc => mgc.CompletedAt);

            // Configure default values
            modelBuilder.Entity<MentorshipGoalCompletion>()
                .Property(mgc => mgc.CompletedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // MentorReview relationships and configurations
            modelBuilder.Entity<MentorReview>()
                .HasOne(mr => mr.MentorshipMatch)
                .WithMany()
                .HasForeignKey(mr => mr.MentorshipMatchId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MentorReview>()
                .HasOne(mr => mr.Mentor)
                .WithMany()
                .HasForeignKey(mr => mr.MentorId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<MentorReview>()
                .HasOne(mr => mr.Mentee)
                .WithMany()
                .HasForeignKey(mr => mr.MenteeId)
                .OnDelete(DeleteBehavior.NoAction);

            // Ensure one review per mentorship match
            modelBuilder.Entity<MentorReview>()
                .HasIndex(mr => mr.MentorshipMatchId)
                .IsUnique();

            // Add indexes for better performance
            modelBuilder.Entity<MentorReview>()
                .HasIndex(mr => mr.MentorId);

            modelBuilder.Entity<MentorReview>()
                .HasIndex(mr => mr.MenteeId);

            modelBuilder.Entity<MentorReview>()
                .HasIndex(mr => mr.CreatedAt);

            // Configure default values
            modelBuilder.Entity<MentorReview>()
                .Property(mr => mr.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Contract relationships and configurations
            modelBuilder.Entity<Contract>()
                .HasOne(c => c.Project)
                .WithOne(p => p.Contract)
                .HasForeignKey<Contract>(c => c.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Contract>()
                .HasOne(c => c.Bidding)
                .WithMany()
                .HasForeignKey(c => c.BiddingId)
                .OnDelete(DeleteBehavior.NoAction);

            // Contract indexes
            modelBuilder.Entity<Contract>()
                .HasIndex(c => c.ProjectId)
                .IsUnique();

            modelBuilder.Entity<Contract>()
                .HasIndex(c => c.Status);

            modelBuilder.Entity<Contract>()
                .HasIndex(c => c.CreatedAt);

            // ContractAuditLog relationships
            modelBuilder.Entity<ContractAuditLog>()
                .HasOne(cal => cal.Contract)
                .WithMany(c => c.AuditLogs)
                .HasForeignKey(cal => cal.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContractAuditLog>()
                .HasOne(cal => cal.User)
                .WithMany()
                .HasForeignKey(cal => cal.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ContractAuditLog>()
                .HasIndex(cal => cal.ContractId);

            modelBuilder.Entity<ContractAuditLog>()
                .HasIndex(cal => cal.Timestamp);

            // ContractRevision relationships
            modelBuilder.Entity<ContractRevision>()
                .HasOne(cr => cr.Contract)
                .WithMany(c => c.Revisions)
                .HasForeignKey(cr => cr.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContractRevision>()
                .HasOne(cr => cr.CreatedByUser)
                .WithMany()
                .HasForeignKey(cr => cr.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ContractRevision>()
                .HasIndex(cr => new { cr.ContractId, cr.RevisionNumber })
                .IsUnique();

            // ContractTemplate indexes
            modelBuilder.Entity<ContractTemplate>()
                .HasIndex(ct => ct.Category);

            modelBuilder.Entity<ContractTemplate>()
                .HasIndex(ct => ct.IsActive);

            // Removed IsDefault index as it's no longer used

            // Configure default values for contracts
            modelBuilder.Entity<Contract>()
                .Property(c => c.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<ContractAuditLog>()
                .Property(cal => cal.Timestamp)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<ContractRevision>()
                .Property(cr => cr.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<ContractTemplate>()
                .Property(ct => ct.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // ContractTermination relationships and configurations
            modelBuilder.Entity<ContractTermination>()
                .HasOne(ct => ct.Contract)
                .WithMany()
                .HasForeignKey(ct => ct.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            // ContractTermination indexes
            modelBuilder.Entity<ContractTermination>()
                .HasIndex(ct => ct.ContractId);

            modelBuilder.Entity<ContractTermination>()
                .HasIndex(ct => ct.Status);

            modelBuilder.Entity<ContractTermination>()
                .HasIndex(ct => ct.RequestedAt);

            modelBuilder.Entity<ContractTermination>()
                .HasIndex(ct => ct.RequestedByUserId);

            // ContractTerminationAuditLog relationships
            modelBuilder.Entity<ContractTerminationAuditLog>()
                .HasOne(ctal => ctal.ContractTermination)
                .WithMany(ct => ct.AuditLogs)
                .HasForeignKey(ctal => ctal.ContractTerminationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ContractTerminationAuditLog>()
                .HasOne(ctal => ctal.User)
                .WithMany()
                .HasForeignKey(ctal => ctal.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ContractTerminationAuditLog>()
                .HasIndex(ctal => ctal.ContractTerminationId);

            modelBuilder.Entity<ContractTerminationAuditLog>()
                .HasIndex(ctal => ctal.Timestamp);

            // Configure default values for contract termination
            modelBuilder.Entity<ContractTermination>()
                .Property(ct => ct.RequestedAt)
                .HasDefaultValueSql("GETUTCDATE()");

                    modelBuilder.Entity<ContractTerminationAuditLog>()
            .Property(ctal => ctal.Timestamp)
            .HasDefaultValueSql("GETUTCDATE()");

        // Configure decimal precision for FinalPayment
        modelBuilder.Entity<ContractTermination>()
            .Property(ct => ct.FinalPayment)
            .HasPrecision(18, 2);

            // Deliverable relationships and configurations
            modelBuilder.Entity<Deliverable>()
                .HasOne(d => d.Contract)
                .WithMany()
                .HasForeignKey(d => d.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Deliverable>()
                .HasOne(d => d.SubmittedByUser)
                .WithMany()
                .HasForeignKey(d => d.SubmittedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Deliverable>()
                .HasOne(d => d.ReviewedByUser)
                .WithMany()
                .HasForeignKey(d => d.ReviewedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Deliverable>()
                .HasOne(d => d.PreviousVersion)
                .WithMany()
                .HasForeignKey(d => d.PreviousVersionId)
                .OnDelete(DeleteBehavior.NoAction);

            // Deliverable indexes
            modelBuilder.Entity<Deliverable>()
                .HasIndex(d => d.ContractId);

            modelBuilder.Entity<Deliverable>()
                .HasIndex(d => d.SubmittedByUserId);

            modelBuilder.Entity<Deliverable>()
                .HasIndex(d => d.Status);

            modelBuilder.Entity<Deliverable>()
                .HasIndex(d => d.SubmittedAt);

            // Configure default values for deliverables
            modelBuilder.Entity<Deliverable>()
                .Property(d => d.SubmittedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // ChatRoom relationships and configurations
            modelBuilder.Entity<ChatRoom>()
                .HasOne(cr => cr.User1)
                .WithMany()
                .HasForeignKey(cr => cr.User1Id)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatRoom>()
                .HasOne(cr => cr.User2)
                .WithMany()
                .HasForeignKey(cr => cr.User2Id)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChatRoom>()
                .HasOne(cr => cr.Project)
                .WithMany()
                .HasForeignKey(cr => cr.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ChatRoom>()
                .HasOne(cr => cr.MentorshipMatch)
                .WithMany()
                .HasForeignKey(cr => cr.MentorshipMatchId)
                .OnDelete(DeleteBehavior.SetNull);

            // ChatMessage relationships and configurations
            modelBuilder.Entity<ChatMessage>()
                .HasOne(cm => cm.ChatRoom)
                .WithMany(cr => cr.Messages)
                .HasForeignKey(cm => cm.ChatRoomId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(cm => cm.Sender)
                .WithMany()
                .HasForeignKey(cm => cm.SenderId)
                .OnDelete(DeleteBehavior.NoAction);

            // ChatFile relationships and configurations
            modelBuilder.Entity<ChatFile>()
                .HasOne(cf => cf.Message)
                .WithMany()
                .HasForeignKey(cf => cf.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            // Add indexes for better performance
            modelBuilder.Entity<ChatRoom>()
                .HasIndex(cr => new { cr.User1Id, cr.User2Id });

            modelBuilder.Entity<ChatRoom>()
                .HasIndex(cr => cr.ProjectId);

            modelBuilder.Entity<ChatRoom>()
                .HasIndex(cr => cr.MentorshipMatchId);

            modelBuilder.Entity<ChatRoom>()
                .HasIndex(cr => cr.LastActivityAt);

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(cm => cm.ChatRoomId);

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(cm => cm.SenderId);

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(cm => cm.SentAt);

            modelBuilder.Entity<ChatMessage>()
                .HasIndex(cm => cm.IsRead);

            modelBuilder.Entity<ChatFile>()
                .HasIndex(cf => cf.MessageId);

            // FreelancerFeedback relationships and configurations
            modelBuilder.Entity<FreelancerFeedback>()
                .HasOne(ff => ff.AcceptBidding)
                .WithMany()
                .HasForeignKey(ff => ff.AcceptBidId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FreelancerFeedback>()
                .HasOne(ff => ff.Freelancer)
                .WithMany()
                .HasForeignKey(ff => ff.FreelancerId)
                .OnDelete(DeleteBehavior.NoAction);

            // FreelancerFeedback indexes
            modelBuilder.Entity<FreelancerFeedback>()
                .HasIndex(ff => ff.AcceptBidId)
                .IsUnique(); // One feedback per accepted bid

            modelBuilder.Entity<FreelancerFeedback>()
                .HasIndex(ff => ff.FreelancerId);

            modelBuilder.Entity<FreelancerFeedback>()
                .HasIndex(ff => ff.CreatedAt);

            // Configure default values for freelancer feedback
            modelBuilder.Entity<FreelancerFeedback>()
                .Property(ff => ff.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // HiringOutcome relationships and configurations
            modelBuilder.Entity<HiringOutcome>()
                .HasIndex(ho => ho.ProjectId);

            modelBuilder.Entity<HiringOutcome>()
                .HasIndex(ho => ho.FreelancerId);

            modelBuilder.Entity<HiringOutcome>()
                .HasIndex(ho => ho.RecordedAt);

            // Configure default values for hiring outcomes
            modelBuilder.Entity<HiringOutcome>()
                .Property(ho => ho.RecordedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Portfolio relationships and configurations
            modelBuilder.Entity<Portfolio>()
                .HasOne(p => p.User)
                .WithMany(u => u.Portfolios)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Portfolio>()
                .HasIndex(p => p.UserId);

            base.OnModelCreating(modelBuilder);
        }
    }
}
