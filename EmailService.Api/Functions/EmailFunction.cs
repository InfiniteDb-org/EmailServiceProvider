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
            _logger.LogInformation("Received email message payload: {MessageBody}", messageBody);

            try
            {
                var emailRequest = JsonConvert.DeserializeObject<EmailSendRequest>(messageBody);
                
                if (emailRequest == null)
                {
                    _logger.LogError("Failed to deserialize email request");
                    throw new InvalidOperationException("Unable to deserialize email request");
                }
                else
                {
                    _logger.LogInformation("Deserialized EmailSendRequest - Subject: {Subject}, PlainText: {PlainText}, Html: {Html}", 
                        emailRequest.Subject, emailRequest.PlainText, emailRequest.Html);
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
                throw; 
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

                // Using the HTML provided by the request (from AccountService)
                var htmlBody = request.Html;

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