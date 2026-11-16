using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UserService.UnitTests.GeneralMocks;
using UsersService.Application.NotificationService.Abstractions;
using UsersService.Application.NotificationService.Enums;
using UsersService.Application.NotificationService.Features.Validation.EmailValidator;
using UsersService.Application.NotificationService.Models;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.NotificationService.Services;
using UsersService.Application.UsersService.Exceptions;
using UsersService.Application.UsersService.Models.Settings;

namespace UserService.UnitTests.NotificationService.Services
{
    public class EmailServiceTests
    {
        private readonly ISmtpClient _smtpClient;
        private readonly INotificationFactory _factory;
        private readonly INotificationService<EmailDto> _factoryEmailService;
        private readonly IValidator<EmailDto> _validator;
        private readonly LoggerSpy<EmailService<EmailDto>> _spy;
        private readonly NotificationSettings _settings;
        private readonly EmailService<EmailDto> _service;

        public EmailServiceTests()
        {
            _smtpClient = Substitute.For<ISmtpClient>();
            _factory = Substitute.For<INotificationFactory>();
            _factoryEmailService = Substitute.For<INotificationService<EmailDto>>();
            _validator = Substitute.For<IValidator<EmailDto>>();
            _spy = new LoggerSpy<EmailService<EmailDto>>();
            _settings = new NotificationSettings
            {
                Name = "TestSender",
                EmailAddress = "sender@test.com",
                Host = "smtp.test.com",
                Port = 587
            };

            _factory.GetService<EmailDto>(NotificationServiceTypes.EmailService)
                .Returns(_factoryEmailService);
            _factoryEmailService.GetValidator()
                .Returns(_validator);

            _service = new EmailService<EmailDto>(
                _spy.Logger,
                Options.Create(_settings),
                _smtpClient,
                _factory);
        }

        // ───────────── Helpers ─────────────

        private EmailDto CreateValidDto(int recipientCount = 1)
        {
            var dto = new EmailDto
            {
                Sender = { Name = "Sender", EmailAddress = "s@test.com" },
                Subject = "Test Subject",
                Text = "Hello"
            };

            for (int i = 0; i < recipientCount; i++)
            {
                dto.Recipients.Add(new EmailContact
                {
                    Name = $"User{i}",
                    EmailAddress = $"user{i}@test.com"
                });
            }

            return dto;
        }

        // ═══════════════════ CreateDto() ═══════════════════

        [Fact]
        public void CreateDto_NoArgs_SetsSenderFromSettings()
        {
            var result = _service.CreateDto();

            result.Should().NotBeNull();
            result.Sender.Name.Should().Be("TestSender");
            result.Sender.EmailAddress.Should().Be("sender@test.com");
        }

        // ═══════════════════ CreateDto(NotificationDto) ═══════════════════

        [Fact]
        public void CreateDto_WithNotificationDto_EmptySender_FillsFromSettings()
        {
            var notification = new NotificationDto
            {
                Sender = { Name = string.Empty, EmailAddress = string.Empty },
                Subject = "Hello",
                Text = "Body"
            };

            var result = _service.CreateDto(notification);

            result.Sender.Name.Should().Be("TestSender");
            result.Sender.EmailAddress.Should().Be("sender@test.com");
        }

        [Fact]
        public void CreateDto_WithNotificationDto_NonEmptySender_KeepsOriginal()
        {
            var notification = new NotificationDto
            {
                Sender = { Name = "Custom", EmailAddress = "custom@test.com" },
                Subject = "Hello",
                Text = "Body"
            };

            var result = _service.CreateDto(notification);

            result.Sender.Name.Should().Be("Custom");
            result.Sender.EmailAddress.Should().Be("custom@test.com");
        }

        // ═══════════════════ GetValidator ═══════════════════

        [Fact]
        public void GetValidator_ReturnsEmailDtoValidator()
        {
            var validator = _service.GetValidator();

            validator.Should().NotBeNull();
            validator.Should().BeOfType<EmailDtoValidator>();
        }

        // ═══════════════════ SendTextAsync ═══════════════════

        [Fact]
        public void SendTextAsync_ValidationFails_ThrowsValidationException()
        {
            var dto = CreateValidDto();
            _validator.Validate(Arg.Any<EmailDto>())
                .Returns(new ValidationResult(new[]
                {
                new ValidationFailure("EmailAddress", "Invalid email")
                }));

            var act = () => _service.SendTextAsync(dto);

            act.Should().ThrowAsync<ValidationException>()
                .Result.Which.Errors.Should().ContainSingle()
                .Which.PropertyName.Should().Be("EmailAddress");
        }

        [Fact]
        public async Task SendTextAsync_Success_SendsToAllRecipients()
        {
            var dto = CreateValidDto(recipientCount: 3);
            _validator.Validate(Arg.Any<EmailDto>())
                .Returns(new ValidationResult()); // валидно

            _smtpClient.SendAsync(
                    Arg.Any<MimeMessage>(),
                    Arg.Any<System.Threading.CancellationToken>(),
                    Arg.Any<MailKit.ITransferProgress>())
                .Returns("message-id");

            await _service.SendTextAsync(dto);

            // 3 recipient → 3 вызова SendAsync
            await _smtpClient.Received(3)
                .SendAsync(
                    Arg.Any<MimeMessage>(),
                    Arg.Any<System.Threading.CancellationToken>(),
                    Arg.Any<MailKit.ITransferProgress>());

            _spy.Has(LogLevel.Information, "Try send message").Should().BeTrue();
            _spy.Has(LogLevel.Information, "Message was send").Should().BeTrue();
        }

        [Fact]
        public async Task SendTextAsync_SmtpFails_ThrowsServiceUnavailableException()
        {
            var dto = CreateValidDto();
            _validator.Validate(Arg.Any<EmailDto>())
                .Returns(new ValidationResult());

            _smtpClient.SendAsync(
                    Arg.Any<MimeMessage>(),
                    Arg.Any<System.Threading.CancellationToken>(),
                    Arg.Any<MailKit.ITransferProgress>())
                .ThrowsAsync(new Exception("Connection refused"));

            var act = () => _service.SendTextAsync(dto);

            var ex = await act.Should().ThrowAsync<ServiceUnavailableException>();
            ex.Which.Message.Should().Contain("SMTP");
            ex.Which.InnerException.Should().NotBeNull();

            _spy.Has(LogLevel.Error, "SMTP service unavailable").Should().BeTrue();
        }
    }
}
