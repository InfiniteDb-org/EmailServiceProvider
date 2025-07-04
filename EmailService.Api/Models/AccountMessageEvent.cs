namespace EmailService.Api.Models;

public class AccountMessageEvent
{
    public string EventType { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string? Email { get; set; }
    public string? Token { get; set; }
    public string? Code { get; set; }
}
