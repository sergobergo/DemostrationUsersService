using MailKit.Net.Smtp;
using MassTransit;
using Microsoft.Extensions.Logging;
using UsersService.Application.NotificationService.Abstractions;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.UsersService.Exceptions;

namespace UsersService.Application.NotificationService.Services.Consumers
{

    public class EmailConsumer : IConsumer<EmailDto>
    {
        private readonly ISmtpConnectionService _smtpRetryer;
        private readonly ILogger<EmailConsumer> _logger;
        private readonly INotificationService<EmailDto> _notificationService;
        private readonly ISmtpClient _smtpClient;

        public EmailConsumer(
            ISmtpConnectionService smtpRetryer,
            INotificationService<EmailDto> notificationService,
            ISmtpClient smtpClient,
            ILogger<EmailConsumer> logger)
        {
            _smtpRetryer = smtpRetryer;
            _notificationService = notificationService;
            _smtpClient = smtpClient;
            _logger = logger;
        }
        public async Task Consume(ConsumeContext<EmailDto> context)
        {
            
            _logger.LogInformation($"EmailConsumer took the message");
            var message = context.Message;
            if (!_smtpClient.IsConnected 
                    ||(_smtpClient.Capabilities.HasFlag(SmtpCapabilities.Authentication) 
                        && !_smtpClient.IsAuthenticated))
            {
                _logger.LogWarning("SMTP connection lost. Reconnecting...");
                await _smtpRetryer.CheckConnectionAsync();
            }
            if ((_smtpClient.IsConnected 
                    && _smtpClient.Capabilities.HasFlag(SmtpCapabilities.Authentication)
                    && _smtpClient.IsAuthenticated)
                || (_smtpClient.IsConnected 
                    && _smtpClient.Capabilities.HasFlag(SmtpCapabilities.Authentication) == false))
            {
                await _notificationService.SendTextAsync(message);
            }
            else
            {
                _logger.LogError("SMTP service unavailable.");
                throw new ServiceUnavailableException("SMTP service unavailable.");
            }
        }
    }
}
