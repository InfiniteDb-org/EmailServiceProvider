using EmailService.Api.Messaging;
using EmailService.Api.Models;
using EmailService.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EmailService.Api.Functions;

public class EmailFunction(ILogger<EmailFunction> logger, IEmailService emailService)
{
    private readonly ILogger<EmailFunction> _logger = logger;
    private readonly IEmailService _emailService = emailService;

    [Function("ProcessEmailVerificationQueue")]
    public async Task ProcessEmailVerificationQueue(
        [ServiceBusTrigger("email-verification", Connection = "ASB_ConnectionString")] string messageBody)
    {
        VerificationCodeSentEvent? evt = null;
        try
        {
            evt = JsonConvert.DeserializeObject<VerificationCodeSentEvent>(messageBody);
            if (evt == null)
            {
                _logger.LogError("Deserialization failed: evt is null. messageBody: {MessageBody}", messageBody);
                return;
            }
            if (string.IsNullOrEmpty(evt.Email))
            {
                _logger.LogError("Invalid VerificationCodeSentEvent: Email is null or empty. {@evt}", evt);
                return;
            }
            if (evt.Code == 0)
            {
                _logger.LogWarning("VerificationCodeSentEvent: Code is 0. {@evt}", evt);
            }

            var emailRequest = EmailRequestFactory.CreateVerificationEmail(evt.Email, evt.Code.ToString());
            var success = await _emailService.SendEmailAsync(emailRequest);

            if (success)
                _logger.LogInformation("Verification email sent successfully to {Recipient}", evt.Email);
            else
                _logger.LogError("Failed to send verification email to {Recipient}", evt.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EXCEPTION] messageBody: {MessageBody}, evt: {@evt}, Exception: {ExceptionMessage}", messageBody, evt, ex.Message);
            throw;
        }
    }
}