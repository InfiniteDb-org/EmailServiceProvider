using Azure;
using Azure.Communication.Email;
using EmailService.Api.Models;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace EmailService_Tests;

public class EmailServiceTests
{
    [Fact]
    public async Task SendEmailAsync_ShouldReturnTrue_WhenEmailSendCompletes()
    {
        // Arrange
        var mockClient = new Mock<EmailClient>();
        var config = new ConfigurationBuilder().AddInMemoryCollection([
            new KeyValuePair<string, string?>("ACS_SenderAddress", "sender@acstest.com")
        ]).Build();

        var request = new EmailSendRequest
        {
            Recipients = ["to@example.com"],
            Subject = "Test Subject",
            PlainText = "Test plain text",
            Html = "<b>Test HTML</b>"
        };

        // Mock SendAsync to return a completed operation
        mockClient.Setup(c => c.SendAsync(
            It.IsAny<WaitUntil>(),
            It.IsAny<EmailMessage>(),
            CancellationToken.None)).ReturnsAsync(Mock.Of<EmailSendOperation>(op => op.HasCompleted == true));

        var service = new EmailService.Api.Services.EmailService(config, mockClient.Object);

        // Act
        var result = await service.SendEmailAsync(request);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SendEmailAsync_ShouldReturnFalse_WhenEmailSendFails()
    {
        // Arrange
        var mockClient = new Mock<EmailClient>();
        var config = new ConfigurationBuilder().AddInMemoryCollection([
            new KeyValuePair<string, string?>("ACS_SenderAddress", "sender@acstest.com")
        ]).Build();

        var request = new EmailSendRequest
        {
            Recipients = ["to@example.com"],
            Subject = "Test Subject",
            PlainText = "Test plain text",
            Html = "<b>Test HTML</b>"
        };

        // Mock SendAsync to return a not-completed operation
        mockClient.Setup(c => c.SendAsync(
            It.IsAny<WaitUntil>(),
            It.IsAny<EmailMessage>(),
            CancellationToken.None)).ReturnsAsync(Mock.Of<EmailSendOperation>(op => op.HasCompleted == false));

        var service = new EmailService.Api.Services.EmailService(config, mockClient.Object);

        // Act
        var result = await service.SendEmailAsync(request);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendEmailAsync_ShouldReturnTrue_WhenEmailSendCompletes_AndVerifyAllProperties()
    {
        // Arrange
        var mockClient = new Mock<EmailClient>();
        var config = new ConfigurationBuilder().AddInMemoryCollection([
            new KeyValuePair<string, string?>("ACS_SenderAddress", "sender@acstest.com")
        ]).Build();

        var request = new EmailSendRequest
        {
            Recipients = ["to@example.com"],
            Subject = "Test Subject",
            PlainText = "Test plain text",
            Html = "<b>Test HTML</b>"
        };

        EmailSendOperation op = Mock.Of<EmailSendOperation>(o => o.HasCompleted == true);
        EmailMessage? capturedMessage = null;
        mockClient.Setup(c => c.SendAsync(
            It.IsAny<WaitUntil>(),
            It.IsAny<EmailMessage>(),
            CancellationToken.None))
            .Callback<WaitUntil, EmailMessage, CancellationToken>((_, msg, _) => capturedMessage = msg)
            .ReturnsAsync(op);

        var service = new EmailService.Api.Services.EmailService(config, mockClient.Object);

        // Act
        var result = await service.SendEmailAsync(request);

        // Assert
        Assert.True(result);
        Assert.NotNull(capturedMessage);
        Assert.Equal("sender@acstest.com", capturedMessage!.SenderAddress);
        Assert.Equal("Test Subject", capturedMessage.Content.Subject);
        Assert.Equal("Test plain text", capturedMessage.Content.PlainText);
        Assert.Equal("<b>Test HTML</b>", capturedMessage.Content.Html);
        Assert.Single(capturedMessage.Recipients.To);
        Assert.Equal("to@example.com", capturedMessage.Recipients.To[0].Address);
    }

    [Fact]
    public async Task SendEmailAsync_ShouldReturnFalse_WhenExceptionIsThrown()
    {
        // Arrange
        var mockClient = new Mock<EmailClient>();
        var config = new ConfigurationBuilder().AddInMemoryCollection([
            new KeyValuePair<string, string?>("ACS_SenderAddress", "sender@acstest.com")
        ]).Build();

        var request = new EmailSendRequest
        {
            Recipients = ["to@example.com"],
            Subject = "Test Subject",
            PlainText = "Test plain text",
            Html = "<b>Test HTML</b>"
        };

        mockClient.Setup(c => c.SendAsync(
            It.IsAny<WaitUntil>(),
            It.IsAny<EmailMessage>(),
            CancellationToken.None)).ThrowsAsync(new Exception("Network error"));

        var service = new EmailService.Api.Services.EmailService(config, mockClient.Object);

        // Act
        bool result;
        try
        {
            result = await service.SendEmailAsync(request);
        }
        catch
        {
            // Om implementationen inte hanterar exception, faila testet
            Assert.Fail("Exception should be handled by EmailService");
            return;
        }
        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SendEmailAsync_ShouldThrow_OnInvalidSenderAddress(string? senderAddress)
    {
        // Arrange
        var mockClient = new Mock<EmailClient>();
        var config = new ConfigurationBuilder().AddInMemoryCollection([
            new KeyValuePair<string, string?>("ACS_SenderAddress", senderAddress)
        ]).Build();

        var request = new EmailSendRequest
        {
            Recipients = ["to@example.com"],
            Subject = "Test Subject",
            PlainText = "Test plain text",
            Html = "<b>Test HTML</b>"
        };
        var service = new EmailService.Api.Services.EmailService(config, mockClient.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SendEmailAsync(request));
    }

    [Fact]
    public async Task SendEmailAsync_ShouldSupportMultipleRecipients()
    {
        // Arrange
        var mockClient = new Mock<EmailClient>();
        var config = new ConfigurationBuilder().AddInMemoryCollection([
            new KeyValuePair<string, string?>("ACS_SenderAddress", "sender@acstest.com")
        ]).Build();

        var recipients = new List<string> { "to1@example.com", "to2@example.com" };
        var request = new EmailSendRequest
        {
            Recipients = recipients,
            Subject = "Test Subject",
            PlainText = "Test plain text",
            Html = "<b>Test HTML</b>"
        };

        EmailSendOperation op = Mock.Of<EmailSendOperation>(o => o.HasCompleted == true);
        EmailMessage? capturedMessage = null;
        mockClient.Setup(c => c.SendAsync(
            It.IsAny<WaitUntil>(),
            It.IsAny<EmailMessage>(),
            CancellationToken.None))
            .Callback<WaitUntil, EmailMessage, CancellationToken>((_, msg, _) => capturedMessage = msg)
            .ReturnsAsync(op);

        var service = new EmailService.Api.Services.EmailService(config, mockClient.Object);

        // Act
        var result = await service.SendEmailAsync(request);

        // Assert
        Assert.True(result);
        Assert.NotNull(capturedMessage);
        Assert.Equal(2, capturedMessage!.Recipients.To.Count);
        Assert.Contains(capturedMessage.Recipients.To, r => r.Address == "to1@example.com");
        Assert.Contains(capturedMessage.Recipients.To, r => r.Address == "to2@example.com");
    }
}
