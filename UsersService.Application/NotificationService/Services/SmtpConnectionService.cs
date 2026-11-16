using MailKit.Net.Smtp;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UsersService.Application.NotificationService.Abstractions;
using UsersService.Application.UsersService.Models.Settings;

namespace UsersService.Application.Services
{
    public class SmtpConnectionService : IHostedService, ISmtpConnectionService, IDisposable
    {
        private readonly ILogger<SmtpConnectionService> _logger;
        private readonly ISmtpClient _smtpClient;
        private readonly NotificationSettings _settings;
        private Timer? _reconnectTimer;
        public SmtpConnectionService(
            ILogger<SmtpConnectionService> logger,
            IOptions<NotificationSettings> options,
            ISmtpClient smtpClient)
        {
            _logger = logger;
            _settings = options.Value;
            _smtpClient = smtpClient;
        }

        public async Task StartAsync(CancellationToken ct)
        {
            _logger.LogInformation($"Connecting to SMTP: {_settings.Host}:{_settings.Port}");
            await ConnectWithRetryAsync(ct);
            _reconnectTimer = new Timer(
            async _ => await CheckConnectionAsync(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _reconnectTimer?.Change(Timeout.Infinite, 0);
            await DisconnectSafelyAsync();
        }
        private async Task DisconnectSafelyAsync()
        {
            try
            {
                if (_smtpClient.IsConnected)
                {
                    await _smtpClient.DisconnectAsync(true);
                }
                _logger.LogInformation("SMTP disconnected gracefully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during SMTP disconnect");
            }
        }
        private async Task ConnectWithRetryAsync(CancellationToken ct)
        {
            const int maxAttempts = 5;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        _logger.LogWarning("Connection or authentication failed...");
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                    }
                    _logger.LogInformation("Try connection to SMTP server...");
                    await _smtpClient.ConnectAsync(
                        _settings.Host,
                        _settings.Port);
                    _logger.LogInformation("SMTP server was connect.");

                    if (_smtpClient.Capabilities.HasFlag(SmtpCapabilities.Authentication))
                    { 
                            _logger.LogInformation("Try authenticate on SMTP server...");
                        await _smtpClient.AuthenticateAsync(
                            _settings.EmailAddress,
                            _settings.EmailPassword,
                            ct);
                        _logger.LogInformation("Application was authenticate on SMTP server.");
                    }
                    return;
                }
                catch (Exception ex) when (
                _smtpClient.Capabilities.HasFlag(SmtpCapabilities.Authentication) 
                && !_smtpClient.IsAuthenticated 
                && _smtpClient.IsConnected)
                {
                    throw new InvalidOperationException("Authenticated on SMTP server failed.", ex);
                }
                catch (Exception ex) when (attempt >= maxAttempts - 1)
                {
                    throw new InvalidOperationException("Failed to connect to SMTP server after multiple attempts.", ex);
                }
            }
        }
        public async Task CheckConnectionAsync()
        {
            try
            {
                if (!_smtpClient.IsConnected ||
                    (_smtpClient.Capabilities.HasFlag(SmtpCapabilities.Authentication) && !_smtpClient.IsAuthenticated))
                {
                    _logger.LogWarning("SMTP connection lost. Reconnecting...");
                    await DisconnectSafelyAsync();
                    await ConnectWithRetryAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect to SMTP server");
            }
        }
        public void Dispose()
        {
            _reconnectTimer?.Dispose();
            _smtpClient.Dispose();
        }

    }
}
