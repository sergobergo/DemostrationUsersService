using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using UserService.UnitTests.GeneralMocks;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.Services;
using UsersService.Application.UsersService.Exceptions;

namespace UserService.UnitTests.NotificationService.Services
{
    public class MessageExchangerServiceTests
    {
        private readonly ISendEndpointProvider _endpointProvider;
        private readonly ISendEndpoint _endpoint;
        private readonly LoggerSpy<MessageExchangerService> _spy;
        private readonly MessageExchangerService _service;

        // Фейковый delay — возвращает мгновенно
        private static Task FakeDelay(TimeSpan _, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public MessageExchangerServiceTests()
        {
            _endpointProvider = Substitute.For<ISendEndpointProvider>();
            _endpoint = Substitute.For<ISendEndpoint>();
            _spy = new LoggerSpy<MessageExchangerService>();

            _endpointProvider.GetSendEndpoint(Arg.Any<Uri>())
                .Returns(_endpoint);

            // ← передаём фейковый delay
            _service = new MessageExchangerService(
                _endpointProvider, _spy.Logger, FakeDelay);
        }

        private EmailDto CreateEmail() => new()
        {
            Recipients = new List<EmailContact>() 
            { new EmailContact() 
                {
                    Name = "userTest",
                    EmailAddress = "user@test.com"
                } 
            },
            Subject = "Test",
            Text = "Hello"
        };

        [Fact]
        public async Task SendEmailMessageAsync_FirstTrySuccess_SendsAndLogs()
        {
            _endpoint.Send(Arg.Any<EmailDto>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await _service.SendEmailMessageAsync(CreateEmail(), CancellationToken.None);

            await _endpoint.Received(1)
                .Send(Arg.Any<EmailDto>(), Arg.Any<CancellationToken>());
            _spy.Has(LogLevel.Information, "Email message sent to queue").Should().BeTrue();
            _spy.Has(LogLevel.Warning, "one more try").Should().BeFalse();
        }

        [Fact]
        public async Task SendEmailMessageAsync_FailsTwice_ThenSucceeds()
        {
            _endpoint.Send(Arg.Any<EmailDto>(), Arg.Any<CancellationToken>())
                .Returns(
                    Task.FromException(new Exception("Rabbit down")),
                    Task.FromException(new Exception("Rabbit down")),
                    Task.CompletedTask
                );

            await _service.SendEmailMessageAsync(CreateEmail(), CancellationToken.None);

            await _endpoint.Received(3)
                .Send(Arg.Any<EmailDto>(), Arg.Any<CancellationToken>());
            _spy.Records.Count(r => r.Level == LogLevel.Warning).Should().Be(2);
            _spy.Has(LogLevel.Information, "Email message sent to queue").Should().BeTrue();
        }

        [Fact]
        public async Task SendEmailMessageAsync_All4AttemptsFail_ThrowsServiceUnavailable()
        {
            _endpoint.Send(Arg.Any<EmailDto>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new Exception("Rabbit down"));

            var act = () => _service.SendEmailMessageAsync(CreateEmail(), CancellationToken.None);

            var ex = await act.Should().ThrowAsync<ServiceUnavailableException>();
            ex.Which.Message.Should().Contain("RabbitMq");
            ex.Which.InnerException.Should().NotBeNull();

            await _endpoint.Received(4)
                .Send(Arg.Any<EmailDto>(), Arg.Any<CancellationToken>());
            _spy.Records.Count(r => r.Level == LogLevel.Warning).Should().Be(3);
            _spy.Has(LogLevel.Error, "Cannot send message").Should().BeTrue();
        }

        [Fact]
        public async Task SendEmailMessageAsync_CancelledDuringRetry_ThrowsOperationCanceled()
        {
            using var cts = new CancellationTokenSource();

            _endpoint.Send(Arg.Any<EmailDto>(), Arg.Any<CancellationToken>())
                .Returns(callInfo =>
                {
                    cts.Cancel();
                    return Task.FromException(new Exception("Rabbit down"));
                });

            var act = () => _service.SendEmailMessageAsync(CreateEmail(), cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task SendEmailMessageAsync_UsesCorrectQueueUri()
        {
            _endpoint.Send(Arg.Any<EmailDto>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            await _service.SendEmailMessageAsync(CreateEmail(), CancellationToken.None);

            await _endpointProvider.Received(1)
                .GetSendEndpoint(Arg.Is<Uri>(u => u.ToString() == "queue:email-queue"));
        }
    }
}
