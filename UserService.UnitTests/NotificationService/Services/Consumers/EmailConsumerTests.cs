using FluentAssertions;
using MailKit.Net.Smtp;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UserService.UnitTests.GeneralMocks;
using UsersService.Application.NotificationService.Abstractions;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.NotificationService.Services.Consumers;
using UsersService.Application.UsersService.Exceptions;

namespace UserService.UnitTests.NotificationService.Services.Consumers
{
    public class EmailConsumerTests
    {
        private readonly ISmtpConnectionService _smtpRetryer;
        private readonly INotificationService<EmailDto> _notificationService;
        private readonly ISmtpClient _smtpClient;
        private readonly LoggerSpy<EmailConsumer> _spy;
        private readonly EmailConsumer _consumer;

        public EmailConsumerTests()
        {
            _smtpRetryer = Substitute.For<ISmtpConnectionService>();
            _notificationService = Substitute.For<INotificationService<EmailDto>>();
            _smtpClient = Substitute.For<ISmtpClient>();
            _spy = new LoggerSpy<EmailConsumer>();

            _consumer = new EmailConsumer(
                _smtpRetryer, _notificationService, _smtpClient, _spy.Logger);
        }

        // ───────────── Helpers ─────────────

        private ConsumeContext<EmailDto> CreateContext()
        {
            var context = Substitute.For<ConsumeContext<EmailDto>>();
            context.Message.Returns(new EmailDto
            {
                Subject = "Test",
                Text = "Hello"
            });
            return context;
        }

        private void SetupConnectedWithAuth()
        {
            _smtpClient.IsConnected.Returns(true);
            _smtpClient.Capabilities.Returns(SmtpCapabilities.Authentication);
            _smtpClient.IsAuthenticated.Returns(true);
        }

        private void SetupConnectedNoAuth()
        {
            _smtpClient.IsConnected.Returns(true);
            _smtpClient.Capabilities.Returns(SmtpCapabilities.None);
            _smtpClient.IsAuthenticated.Returns(false);
        }

        private void SetupDisconnected()
        {
            _smtpClient.IsConnected.Returns(false);
            _smtpClient.Capabilities.Returns(SmtpCapabilities.None);
            _smtpClient.IsAuthenticated.Returns(false);
        }

        private void SetupConnectedAuthFailed()
        {
            _smtpClient.IsConnected.Returns(true);
            _smtpClient.Capabilities.Returns(SmtpCapabilities.Authentication);
            _smtpClient.IsAuthenticated.Returns(false);
        }

        // ═══════════════════ Тесты ═══════════════════

        [Fact]
        public async Task Consume_ConnectedAndAuthenticated_SendsEmail()
        {
            SetupConnectedWithAuth();
            var context = CreateContext();

            await _consumer.Consume(context);

            await _notificationService.Received(1)
                .SendTextAsync(context.Message);
            await _smtpRetryer.DidNotReceive()
                .CheckConnectionAsync();

            _spy.Has(LogLevel.Information, "took the message").Should().BeTrue();
        }

        [Fact]
        public async Task Consume_ConnectedNoAuthRequired_SendsEmail()
        {
            SetupConnectedNoAuth();
            var context = CreateContext();

            await _consumer.Consume(context);

            await _notificationService.Received(1)
                .SendTextAsync(context.Message);
            await _smtpRetryer.DidNotReceive()
                .CheckConnectionAsync();
        }

        [Fact]
        public async Task Consume_Disconnected_ReconnectsAndSends()
        {
            // До reconnect — отключён
            SetupDisconnected();
            var context = CreateContext();

            // После CheckConnectionAsync — подключён без auth
            _smtpRetryer.CheckConnectionAsync()
                .Returns(Task.CompletedTask)
                .AndDoes(_ => SetupConnectedNoAuth());

            await _consumer.Consume(context);

            await _smtpRetryer.Received(1).CheckConnectionAsync();
            await _notificationService.Received(1)
                .SendTextAsync(context.Message);

            _spy.Has(LogLevel.Warning, "connection lost").Should().BeTrue();
        }

        [Fact]
        public async Task Consume_AuthFailed_ReconnectDoesNotHelp_ThrowsServiceUnavailable()
        {
            // Auth required, но не authenticated
            SetupConnectedAuthFailed();
            var context = CreateContext();

            // CheckConnectionAsync не исправляет ситуацию
            _smtpRetryer.CheckConnectionAsync()
                .Returns(Task.CompletedTask);
            // _smtpClient остаётся в том же состоянии (IsAuthenticated = false)

            var act = () => _consumer.Consume(context);

            await act.Should().ThrowAsync<ServiceUnavailableException>()
                .WithMessage("*SMTP*");

            await _smtpRetryer.Received(1).CheckConnectionAsync();
            await _notificationService.DidNotReceive()
                .SendTextAsync(Arg.Any<EmailDto>());

            _spy.Has(LogLevel.Error, "SMTP service unavailable").Should().BeTrue();
        }
    }
}
