namespace Freelancing.Models.Entities
{
    public class PeerMentorship
    {
        public Guid Id { get; set; }
        public string UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public UserAccount User { get; set; }
    }
}
