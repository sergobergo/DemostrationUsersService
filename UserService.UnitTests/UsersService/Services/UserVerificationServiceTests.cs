using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using UserService.UnitTests.GeneralMocks;
using UsersService.Application.NotificationService.Abstractions;
using UsersService.Application.NotificationService.Enums;
using UsersService.Application.NotificationService.Models;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.UsersService.Abstractions;
using UsersService.Application.UsersService.Exceptions;
using UsersService.Application.UsersService.Models;
using UsersService.Application.UsersService.Services;

namespace UserService.UnitTests.UsersService.Services
{
    public class UserVerificationServiceTests
    {
        private readonly IRedisService _redis;
        private readonly INotificationFactory _notification;
        private readonly IAuthService _authService;
        private readonly IMessageExchanger _messageExchanger;
        private readonly LoggerSpy<UserVerificationService> _spy;
        private readonly UserVerificationService _service;
        private readonly INotificationService<EmailDto> _emailService;

        public UserVerificationServiceTests()
        {
            _redis = Substitute.For<IRedisService>();
            _notification = Substitute.For<INotificationFactory>();
            _authService = Substitute.For<IAuthService>();
            _messageExchanger = Substitute.For<IMessageExchanger>();
            _spy = new LoggerSpy<UserVerificationService>();
            _emailService = Substitute.For<INotificationService<EmailDto>>();

            _notification.GetService<EmailDto>(NotificationServiceTypes.EmailService)
                .Returns(_emailService);
            _emailService.CreateDto(Arg.Any<NotificationDto>())
                .Returns(new EmailDto());

            _service = new UserVerificationService(
                _redis, _notification, _authService, _messageExchanger, _spy.Logger);
        }

        [Fact]
        public async Task RememberUserAndSendCodeAsync_Success_SetsCode_SavesToRedis_SendsEmail()
        {
            // Arrange
            var user = new UserRegDto
            {
                Login = "testuser",
                Email = "test@test.com",
                Password = "Pass123!",
                Name = "Test"
            };
            var ct = CancellationToken.None;

            _redis.SetAuthCodeAsync(Arg.Any<string>(), Arg.Any<UserRegDto>())
                .Returns(Task.CompletedTask);
            _messageExchanger.SendEmailMessageAsync(Arg.Any<EmailDto>(), ct)
                .Returns(Task.CompletedTask);

            // Act
            await _service.RememberUserAndSendCodeAsync(user, ct);

            // Assert
            user.Code.Should().NotBeNullOrEmpty();

            await _redis.Received(1)
                .SetAuthCodeAsync("testuser", Arg.Is<UserRegDto>(u => u.Code == user.Code));

            await _messageExchanger.Received(1)
                .SendEmailMessageAsync(Arg.Any<EmailDto>(), ct);
        }

        [Fact]
        public async Task UserCheckAndLoginAsync_UserNotInRedis_ReturnsNull()
        {
            _redis.GetDataAsync<UserRegDto?>(Arg.Any<string>())
                .Returns(Task.FromResult<UserRegDto?>(null));

            var result = await _service.UserCheckAndLoginAsync("ghost", "123456");

            result.Should().BeNull();
            await _authService.DidNotReceive().AddUserAsync(Arg.Any<UserRegDto>());
        }

        [Fact]
        public async Task UserCheckAndLoginAsync_CodeMismatch_ReturnsNull()
        {
            var storedUser = new UserRegDto
            {
                Login = "testuser",
                Email = "test@test.com",
                Password = "Pass123!",
                Name = "Test",
                Code = "111111"
            };

            _redis.GetDataAsync<UserRegDto?>(Arg.Any<string>())
                .Returns(Task.FromResult<UserRegDto?>(storedUser));

            var result = await _service.UserCheckAndLoginAsync("testuser", "999999");

            result.Should().BeNull();
            await _authService.DidNotReceive().AddUserAsync(Arg.Any<UserRegDto>());
            await _redis.DidNotReceive().RemoveAsync(Arg.Any<string>());
        }

        [Fact]
        public async Task UserCheckAndLoginAsync_CodeMatch_RegistersAndLogins()
        {
            var storedUser = new UserRegDto
            {
                Login = "testuser",
                Email = "test@test.com",
                Password = "Pass123!",
                Name = "Test",
                Code = "123456"
            };
            var expectedLogDto = new UserLogDto { Login = "testuser", Password = "Pass123!" };
            var expectedToken = new TokenResponseDto
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token"
            };

            _redis.GetDataAsync<UserRegDto?>(Arg.Any<string>())
                .Returns(Task.FromResult<UserRegDto?>(storedUser));
            _redis.RemoveAsync(Arg.Any<string>())
                .Returns(Task.CompletedTask);
            _authService.AddUserAsync(Arg.Any<UserRegDto>())
                .Returns(expectedLogDto);
            _authService.LoginAsync(Arg.Any<UserLogDto>())
                .Returns(expectedToken);

            var result = await _service.UserCheckAndLoginAsync("testuser", "123456");

            result.Should().NotBeNull();
            result!.AccessToken.Should().Be("access-token");
            result.RefreshToken.Should().Be("refresh-token");

            await _redis.Received(1).RemoveAsync("testuser");

            await _authService.Received(1).AddUserAsync(storedUser);
            await _authService.Received(1).LoginAsync(expectedLogDto);
        }

        [Fact]
        public async Task RefreshAndSendCodeAsync_UserFound_RegeneratesCodeAndSendsEmail()
        {
            var storedUser = new UserRegDto
            {
                Login = "testuser",
                Email = "test@test.com",
                Password = "Pass123!",
                Name = "Test",
                Code = "old-code"
            };
            var ct = CancellationToken.None;

            _redis.GetDataAsync<UserRegDto?>(Arg.Any<string>())
                .Returns(Task.FromResult<UserRegDto?>(storedUser));
            _redis.RemoveAsync(Arg.Any<string>())
                .Returns(Task.CompletedTask);
            _redis.SetAuthCodeAsync(Arg.Any<string>(), Arg.Any<UserRegDto>())
                .Returns(Task.CompletedTask);
            _messageExchanger.SendEmailMessageAsync(Arg.Any<EmailDto>(), ct)
                .Returns(Task.CompletedTask);

            await _service.RefreshAndSendCodeAsync("testuser", ct);

            storedUser.Code.Should().NotBe("old-code");
            storedUser.Code.Should().NotBeNullOrEmpty();

            await _redis.Received(1).RemoveAsync("testuser");
            await _redis.Received(1)
                .SetAuthCodeAsync("testuser",
                    Arg.Is<UserRegDto>(u => u.Code == storedUser.Code));

            await _messageExchanger.Received(1)
                .SendEmailMessageAsync(Arg.Any<EmailDto>(), ct);
        }

        [Fact]
        public async Task RefreshAndSendCodeAsync_UserNotFound_ThrowsUserNotFoundException()
        {
            _redis.GetDataAsync<UserRegDto?>(Arg.Any<string>())
                .Returns(Task.FromResult<UserRegDto?>(null));

            var act = () => _service.RefreshAndSendCodeAsync("ghost", CancellationToken.None);

            await act.Should().ThrowAsync<UserNotFoundException>()
                .WithMessage("*not found*");

            _spy.Has(LogLevel.Warning, "not found").Should().BeTrue();

            await _messageExchanger.DidNotReceive()
                .SendEmailMessageAsync(Arg.Any<EmailDto>(), Arg.Any<CancellationToken>());
            await _redis.DidNotReceive()
                .SetAuthCodeAsync(Arg.Any<string>(), Arg.Any<UserRegDto>());
        }
    }
}
