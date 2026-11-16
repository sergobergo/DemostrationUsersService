using MassTransit;
using Microsoft.Extensions.Logging;
using UsersService.Application.NotificationService.Abstractions;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.UsersService.Exceptions;

namespace UsersService.Application.Services
{
    public class MessageExchangerService : IMessageExchanger
    {
        private readonly ISendEndpointProvider _sendEndpointProvider;
        private readonly ILogger<MessageExchangerService> _logger;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;

        public MessageExchangerService(
            ISendEndpointProvider sendEndpointProvider,
            ILogger<MessageExchangerService> logger,
            Func<TimeSpan, CancellationToken, Task>? delay = null)
        {
            _sendEndpointProvider = sendEndpointProvider;
            _delay = delay ?? Task.Delay;
            _logger = logger;
        }
        public async Task SendEmailMessageAsync(EmailDto email, CancellationToken ct)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    var endpoint = await _sendEndpointProvider.GetSendEndpoint(
                    new Uri("queue:email-queue"));
                    await endpoint.Send(email);
                    _logger.LogInformation("Email message sent to queue");
                    return;
                }
                catch(Exception ex) when (attempt == 3)
                {
                    _logger.LogError(ex, "Cannot send message, RabbitMq service unavailable.");
                    throw new ServiceUnavailableException("RabbitMq service unavailable.", ex);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RabbitMq service unavailable, one more try send message.");
                    await _delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                }
            }
            
        }
    }
}
