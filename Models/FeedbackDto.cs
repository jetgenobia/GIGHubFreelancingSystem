public class FeedbackDto
{
    public Guid Id { get; set; }
    public int Rating { get; set; }
    public bool WouldRecommend { get; set; }
    public string? Comments { get; set; }
    public DateTime CreatedAt { get; set; }
    public UserDto User { get; set; } = new();
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public string? Photo { get; set; }
}
