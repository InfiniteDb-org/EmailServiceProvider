using Azure;
using Azure.Communication.Email;
using EmailService.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EmailService.Api.Services;

public interface IEmailService
{
    Task<bool> SendEmailAsync(EmailSendRequest request);
}

public class EmailService(IConfiguration configuration, EmailClient client, ILogger<EmailService> logger) : IEmailService
{
    private readonly IConfiguration _configuration = configuration;
    private readonly EmailClient _client = client;
    private readonly ILogger<EmailService> _logger = logger;

    public async Task<bool> SendEmailAsync(EmailSendRequest request)
    {
        var senderAddress = _configuration["ACS_SenderAddress"];
        _logger.LogInformation("Using sender address: {SenderAddress}", senderAddress);
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

            var emailSendOperation = await _client.SendAsync(WaitUntil.Completed, emailMessage);
            return emailSendOperation.HasCompleted;
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to send email. Request: {@Request}", request); 
            return false;
        }
    }
}