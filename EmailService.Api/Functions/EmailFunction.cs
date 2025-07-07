using EmailService.Api.Messaging;
using EmailService.Api.Models;
using EmailService.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EmailService.Api.Functions;

public class EmailFunction(ILogger<EmailFunction> logger, IEmailService emailService, EmailRequestFactory emailRequestFactory)
{
    private readonly ILogger<EmailFunction> _logger = logger;
    private readonly IEmailService _emailService = emailService;
    private readonly EmailRequestFactory _emailRequestFactory = emailRequestFactory;

    [Function("ProcessEmailVerificationQueue")]
    public async Task ProcessEmailVerificationQueue(
        [ServiceBusTrigger("verification-code-emails", Connection = "ASB_ConnectionString")] string messageBody)
    {
        VerificationCodeSentEvent? evt = null;
        try
        {
            evt = JsonConvert.DeserializeObject<VerificationCodeSentEvent>(messageBody);
            if (evt?.Email is not { Length: > 0 }) return;
            if (evt.Code == 0) _logger.LogWarning("VerificationCodeSentEvent: Code is 0. {@evt}", evt);

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

    [Function("ProcessAccountEventsQueue")]
    public async Task ProcessAccountEventsQueue(
        [ServiceBusTrigger("account-lifecycle-events", Connection = "ASB_ConnectionString")] string messageBody)
    {
        AccountMessageEvent? evt = null;
        try
        {
            evt = JsonConvert.DeserializeObject<AccountMessageEvent>(messageBody);

            // log event for traceability
            _logger.LogInformation("Received AccountMessageEvent: {@evt}", evt);

            if (evt == null || string.IsNullOrEmpty(evt.Email))
            {
                _logger.LogError("Invalid AccountMessageEvent: Email is null or empty. {@evt}", evt);
                return;
            }

            // build email based on event type
            var emailRequest = evt.EventType switch
            {
                "AccountCreated" => _emailRequestFactory.CreateWelcomeEmail(evt.Email),
                "PasswordResetRequested" => _emailRequestFactory.CreatePasswordResetEmail(evt.Email, evt.ResetToken ?? ""),
                "AccountDeleted" => _emailRequestFactory.CreateAccountDeletedEmail(evt.Email),
                _ => null
            };

            if (emailRequest == null)
            {
                _logger.LogWarning("Unknown EventType: {EventType}. Skipping email.", evt.EventType);
                return;
            }

            // send email and log result
            var success = await _emailService.SendEmailAsync(emailRequest);

            if (success)
                _logger.LogInformation("{EventType} email sent successfully to {Recipient}", evt.EventType, evt.Email);
            else
                _logger.LogError("Failed to send {EventType} email to {Recipient}", evt.EventType, evt.Email);
        }
        catch (Exception ex)
        {
            // log and rethrow on error
            _logger.LogError(ex, "[EXCEPTION] messageBody: {MessageBody}, evt: {@evt}, Exception: {ExceptionMessage}", 
                messageBody, evt, ex.Message);
            throw;
        }
    }
}