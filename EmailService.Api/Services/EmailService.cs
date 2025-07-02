using Azure;
using Azure.Communication.Email;
using EmailService.Api.Models;
using Microsoft.Extensions.Configuration;

namespace EmailService.Api.Services;

public interface IEmailService
{
    Task<bool> SendEmailAsync(EmailSendRequest request);
}

public class EmailService(IConfiguration configuration, EmailClient client) : IEmailService
{
    private readonly IConfiguration _configuration = configuration;
    private readonly EmailClient _client = client;

    public async Task<bool> SendEmailAsync(EmailSendRequest request)
    {
        var senderAddress = _configuration["ACS_SenderAddress"];
        if (string.IsNullOrWhiteSpace(senderAddress))
            throw new InvalidOperationException("Sender address is not configured.");

        try
        {
            var recipients = request.Recipients.Select(recipient => new EmailAddress(recipient)).ToList();

            var emailMessage = new EmailMessage(
                senderAddress: senderAddress,
                content: new EmailContent(request.Subject)
                {
                    PlainText = request.PlainText,
                    Html = request.Html
                },
                recipients: new EmailRecipients(recipients));

            EmailSendOperation emailSendOperation = await _client.SendAsync(WaitUntil.Completed, emailMessage);
            return emailSendOperation.HasCompleted;
        }
        catch
        {
            // Log error if you want, men returnera false
            return false;
        }
    }
}