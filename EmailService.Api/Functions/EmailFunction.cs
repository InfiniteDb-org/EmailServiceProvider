using Azure;
using Azure.Communication.Email;
using EmailService.Api.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EmailService.Api.Functions;

public class EmailFunction(ILogger<EmailFunction> logger, EmailClient emailClient)
{
        private readonly ILogger<EmailFunction> _logger = logger;
        private readonly EmailClient _emailClient = emailClient;
        private readonly string _senderAddress = Environment.GetEnvironmentVariable("ACS_SenderAddress") 
                                                 ?? throw new InvalidOperationException("ACS_SenderAddress environment variable is not set");

        [Function("ProcessEmailQueue")]
        public async Task ProcessEmailQueue(
            [ServiceBusTrigger("email-verification", Connection = "ASB_ConnectionString")]
            string messageBody, FunctionContext context)
        {
            _logger.LogInformation("Processing email message: {MessageBody}", messageBody);

            try
            {
                var emailRequest = JsonConvert.DeserializeObject<EmailSendRequest>(messageBody);
                
                if (emailRequest == null)
                {
                    _logger.LogError("Failed to deserialize email request");
                    throw new InvalidOperationException("Unable to deserialize email request");
                }

                var success = await SendEmailAsync(emailRequest);
                
                if (success)
                {
                    _logger.LogInformation("Email sent successfully to {RecipientCount} recipients", 
                        emailRequest.Recipients?.Count ?? 0);
                }
                else
                {
                    _logger.LogError("Failed to send email");
                    throw new InvalidOperationException("Failed to send email");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing email message");
                throw; // Re-throw to trigger retry mechanism
            }
        }

        private async Task<bool> SendEmailAsync(EmailSendRequest request)
        {
            try
            {
                var recipients = new List<EmailAddress>();

                foreach (var recipient in request.Recipients)
                {
                    recipients.Add(new EmailAddress(recipient));
                }

                var htmlBody = $@"
                    <html>
                        <body style='font-family:sans-serif; background:#f9f9f9; padding:2rem;'>
                            <div style='background:#fff; border-radius:8px; max-width:480px; margin:auto; box-shadow:0 2px 8px #eee; padding:2rem;'>
                                <h2 style='color:#4a90e2;'>Rooms - Verifieringskod</h2>
                                <p style='font-size:16px;'>Hi!</p>
                                <p style='font-size:16px;'>Your verification code is:</p>
                                <div style='font-size:2rem; font-weight:bold; background:#f0f4fa; color:#4a90e2; padding:1rem; border-radius:4px; text-align:center; margin:1rem 0;'>{request.PlainText}</div>
                                <p style='font-size:14px; color:#888;'>If you did not request this code, please ignore this email.</p>
                                <hr style='margin:2rem 0;'/>
                                <div style='font-size:12px; color:#bbb;'>Rooms Chat Platform</div>
                            </div>
                        </body>
                    </html>";

                var emailMessage = new EmailMessage(
                    senderAddress: _senderAddress,
                    content: new EmailContent(request.Subject)
                    {
                        PlainText = request.PlainText, 
                        Html = htmlBody 
                    },
                    recipients: new EmailRecipients(recipients)
                );

                var emailSendOperation = await _emailClient.SendAsync(
                    WaitUntil.Completed, 
                    emailMessage);

                return emailSendOperation.HasCompleted;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email via Azure Communication Service");
                return false;
            }
        }
    }