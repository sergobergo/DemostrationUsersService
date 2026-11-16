using Microsoft.Extensions.Logging;
using UsersService.Application.NotificationService.Abstractions;
using UsersService.Application.NotificationService.Enums;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.UsersService.Abstractions;
using UsersService.Application.UsersService.Exceptions;
using UsersService.Application.UsersService.Features.Mappers;
using UsersService.Application.UsersService.Models;
using UsersService.Application.UsersService.Services.Helpers;

namespace UsersService.Application.UsersService.Services
{
    public class UserVerificationService : IUserVerificationService
    {
        private readonly IRedisService _redis;
        private readonly INotificationFactory _notification;
        private readonly IAuthService _authService;
        private readonly IMessageExchanger _messageExchanger;
        private readonly ILogger<UserVerificationService> _logger;
        public UserVerificationService(
            IRedisService redis,
            INotificationFactory notification,
            IAuthService authService,
            IMessageExchanger messageExchanger,
            ILogger<UserVerificationService> logger
            )
        {
            _redis = redis;
            _authService = authService;
            _notification = notification;
            _messageExchanger = messageExchanger;
            _logger = logger;
        }

        public async Task RememberUserAndSendCodeAsync(UserRegDto user, CancellationToken ct)
        {
            user.Code = AuthCodeGenerator.GenerateCode();
            await _redis.SetAuthCodeAsync(user.Login, user); 
            var emailservice = _notification.GetService<EmailDto>(NotificationServiceTypes.EmailService);
            var message = user.ToNotificationDto();
            message.Subject = "Verification code";
            await _messageExchanger.SendEmailMessageAsync(emailservice.CreateDto(message), ct);
        }

        public async Task<TokenResponseDto?> UserCheckAndLoginAsync(string login, string code)
        {
            var user = await _redis.GetDataAsync<UserRegDto?>(login);
            if (user == null) return null;
            if (user.Code == code)
            {
                await _redis.RemoveAsync(login);
                var newUser = await _authService.AddUserAsync(user);
                var token = await _authService.LoginAsync(newUser);
                return token;
            }
            return null;
        }
        public async Task RefreshAndSendCodeAsync(string login, CancellationToken ct)
        {
            var user = await _redis.GetDataAsync<UserRegDto?>(login);
            if(user is not null)
            {
                user.Code = AuthCodeGenerator.GenerateCode();
                await _redis.RemoveAsync(login);
                await _redis.SetAuthCodeAsync(login, user);
                var emailservice = _notification.GetService<EmailDto>(NotificationServiceTypes.EmailService);
                var notificationDto = user.ToNotificationDto();
                notificationDto.Subject = "Verification code";
                var message = emailservice.CreateDto(notificationDto);
                await _messageExchanger.SendEmailMessageAsync(message, ct);
            }
            else
            {
                _logger.LogWarning("User not found, when refresh verify code.");
                throw new UserNotFoundException("User not found in redis.");
            }
        }

    }
}
