using FluentValidation;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using UsersService.Application.NotificationService.Abstractions;
using UsersService.Application.NotificationService.Enums;
using UsersService.Application.NotificationService.Models;
using UsersService.Application.NotificationService.Features.Mappers;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.NotificationService.Features.Validation.EmailValidator;
using UsersService.Application.UsersService.Models.Settings;
using UsersService.Application.UsersService.Exceptions;


namespace UsersService.Application.NotificationService.Services
{
    public class EmailService<T> : INotificationService<T> where T : EmailDto, new()
    {
        private readonly INotificationFactory _notificationFactory;
        private readonly ILogger<EmailService<T>> _logger;
        private readonly NotificationSettings _settings;
        private readonly ISmtpClient _smtpClient;

        public EmailService
        (
            ILogger<EmailService<T>> logger,
            IOptions<NotificationSettings> options,
            ISmtpClient smtpClient,
            INotificationFactory notificationFactory
        )
        {
            _logger = logger;
            _settings = options.Value;
            _smtpClient = smtpClient;
            _notificationFactory = notificationFactory;
        }
        public T CreateDto()
        {
            var dto = new EmailDto();
            dto.Sender.Name = _settings.Name;
            dto.Sender.EmailAddress = _settings.EmailAddress;
            return (T)dto;
        }

        public T CreateDto(NotificationDto dto)
        {
            if (dto.Sender.Name == string.Empty && dto.Sender.EmailAddress == string.Empty)
            {
                dto.Sender.Name = _settings.Name;
                dto.Sender.EmailAddress = _settings.EmailAddress;
            }
            return (T)dto.ToEmailDto();
        }

        public IValidator<T> GetValidator()
        {
            return new EmailDtoValidator();
        }

        public async Task SendTextAsync(T dto)
        {

            var validator = _notificationFactory.GetService<EmailDto>(NotificationServiceTypes.EmailService).GetValidator();
            var result = validator.Validate(dto);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
            var msg = new MimeMessage();
            var bb = new BodyBuilder();
            _logger.LogInformation("Try send message.");
            try
            {
                for ( var i = 0; i < dto.Recipients.Count; i++)
                {
                    msg.From.Add(new MailboxAddress(dto.Sender.Name, dto.Sender.EmailAddress));
                    msg.To.Add(new MailboxAddress(dto.Recipients[i].Name, dto.Recipients[i].EmailAddress));
                    msg.Subject = dto.Subject;
                    bb.HtmlBody = $"<p>{dto.Text}</p>";
                    msg.Body = bb.ToMessageBody();
                    await _smtpClient.SendAsync(msg);
                    _logger.LogInformation("Message was send");
                } 
            }
            catch(Exception ex)
            {
                _logger.LogError("SMTP service unavailable.");
                throw new ServiceUnavailableException("SMTP service unavailable.", ex);
            }
        }
    }
}

