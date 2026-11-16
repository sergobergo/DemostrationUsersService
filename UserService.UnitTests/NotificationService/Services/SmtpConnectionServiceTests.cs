using FluentAssertions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using UserService.UnitTests.GeneralMocks;
using UsersService.Application.Services;
using UsersService.Application.UsersService.Models.Settings;

namespace UserService.UnitTests.NotificationService.Services
{
    public class SmtpConnectionServiceTests
    {
        private readonly ISmtpClient _smtpClient;          
        private readonly LoggerSpy<SmtpConnectionService> _spy;
        private readonly NotificationSettings _settings;
        private readonly SmtpConnectionService _service;

        public SmtpConnectionServiceTests()
        {
            _smtpClient = Substitute.For<ISmtpClient>();  
            _spy = new LoggerSpy<SmtpConnectionService>();
            _settings = new NotificationSettings
            {
                Host = "smtp.test.com",
                Port = 587,
                EmailAddress = "test@test.com",
                EmailPassword = "password"
            };

            _service = new SmtpConnectionService(
                _spy.Logger,
                Options.Create(_settings),
                _smtpClient);
        }

        // ═══════════════════ StartAsync ═══════════════════

        [Fact]
        public async Task StartAsync_NoAuthRequired_ConnectsSuccessfully()
        {
            _smtpClient.Capabilities.Returns(SmtpCapabilities.None);
            _smtpClient.ConnectAsync(
                    Arg.Any<string>(), Arg.Any<int>(),
                    Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await _service.StartAsync(CancellationToken.None);

            await _smtpClient.Received(1)
                .ConnectAsync("smtp.test.com", 587,
                    SecureSocketOptions.Auto, Arg.Any<CancellationToken>());
            await _smtpClient.DidNotReceive()
                .AuthenticateAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

            _spy.Has(LogLevel.Information, "SMTP server was connect").Should().BeTrue();
        }

        [Fact]
        public async Task StartAsync_AuthRequired_ConnectsAndAuthenticates()
        {
            _smtpClient.Capabilities.Returns(SmtpCapabilities.Authentication);
            _smtpClient.ConnectAsync(
                    Arg.Any<string>(), Arg.Any<int>(),
                    Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            _smtpClient.AuthenticateAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await _service.StartAsync(CancellationToken.None);

            await _smtpClient.Received(1)
                .AuthenticateAsync("test@test.com", "password",
                    Arg.Any<CancellationToken>());

            _spy.Has(LogLevel.Information, "authenticate on SMTP server").Should().BeTrue();
        }

        [Fact]
        public async Task StartAsync_AuthFails_ThrowsInvalidOperationException()
        {
            _smtpClient.Capabilities.Returns(SmtpCapabilities.Authentication);
            _smtpClient.IsConnected.Returns(true);
            _smtpClient.IsAuthenticated.Returns(false);

            _smtpClient.ConnectAsync(
                    Arg.Any<string>(), Arg.Any<int>(),
                    Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            _smtpClient.AuthenticateAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new AuthenticationException("Bad credentials"));

            var act = () => _service.StartAsync(CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Authenticated*failed*");
        }

        // ═══════════════════ StopAsync ═══════════════════

        [Fact]
        public async Task StopAsync_Connected_DisconnectsGracefully()
        {
            _smtpClient.IsConnected.Returns(true);
            _smtpClient.DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await _service.StopAsync(CancellationToken.None);

            await _smtpClient.Received(1)
                .DisconnectAsync(true, Arg.Any<CancellationToken>());
            _spy.Has(LogLevel.Information, "disconnected gracefully").Should().BeTrue();
        }

        [Fact]
        public async Task StopAsync_NotConnected_SkipsDisconnect()
        {
            _smtpClient.IsConnected.Returns(false);

            await _service.StopAsync(CancellationToken.None);

            await _smtpClient.DidNotReceive()
                .DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
            _spy.Has(LogLevel.Information, "disconnected gracefully").Should().BeTrue();
        }

        [Fact]
        public async Task StopAsync_DisconnectThrows_LogsWarning_NotThrows()
        {
            _smtpClient.IsConnected.Returns(true);
            _smtpClient.DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new Exception("Network error"));

            var act = () => _service.StopAsync(CancellationToken.None);

            await act.Should().NotThrowAsync();
            _spy.Has(LogLevel.Warning, "Error during SMTP disconnect").Should().BeTrue();
        }

        // ═══════════════════ CheckConnectionAsync ═══════════════════

        [Fact]
        public async Task CheckConnectionAsync_FullyConnected_NoReconnect()
        {
            _smtpClient.IsConnected.Returns(true);
            _smtpClient.Capabilities.Returns(SmtpCapabilities.Authentication);
            _smtpClient.IsAuthenticated.Returns(true);

            await _service.CheckConnectionAsync();

            await _smtpClient.DidNotReceive()
                .ConnectAsync(Arg.Any<string>(), Arg.Any<int>(),
                    Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task CheckConnectionAsync_Disconnected_Reconnects()
        {
            _smtpClient.IsConnected.Returns(false);
            _smtpClient.Capabilities.Returns(SmtpCapabilities.None);
            _smtpClient.ConnectAsync(
                    Arg.Any<string>(), Arg.Any<int>(),
                    Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await _service.CheckConnectionAsync();

            _spy.Has(LogLevel.Warning, "connection lost").Should().BeTrue();
            await _smtpClient.Received(1)
                .ConnectAsync(Arg.Any<string>(), Arg.Any<int>(),
                    Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task CheckConnectionAsync_ReconnectFails_LogsError_NotThrows()
        {
            _smtpClient.IsConnected.Returns(true);
            _smtpClient.Capabilities.Returns(SmtpCapabilities.Authentication);
            _smtpClient.IsAuthenticated.Returns(false);

            _smtpClient.DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            _smtpClient.ConnectAsync(
                    Arg.Any<string>(), Arg.Any<int>(),
                    Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            _smtpClient.AuthenticateAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new AuthenticationException("Re-auth failed"));

            var act = () => _service.CheckConnectionAsync();

            await act.Should().NotThrowAsync();
            _spy.Has(LogLevel.Error, "Failed to reconnect").Should().BeTrue();
        }

        // ═══════════════════ Dispose ═══════════════════

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            var act = () => _service.Dispose();
            act.Should().NotThrow();
        }
    }
}
