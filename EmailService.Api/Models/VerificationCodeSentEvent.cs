namespace EmailService.Api.Models;

public class VerificationCodeSentEvent
{
    public string Email { get; set; } = null!;
    public int Code { get; set; }
}