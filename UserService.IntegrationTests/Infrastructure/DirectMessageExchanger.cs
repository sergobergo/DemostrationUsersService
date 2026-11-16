using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsersService.Application.NotificationService.Abstractions;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;

namespace UserService.IntegrationTests.Infrastructure
{
    public class DirectMessageExchanger : IMessageExchanger
    {
        private readonly INotificationService<EmailDto> _emailService;

        public DirectMessageExchanger(INotificationService<EmailDto> emailService)
        {
            _emailService = emailService;
        }

        public async Task SendEmailMessageAsync(EmailDto email, CancellationToken ct)
        {
            await _emailService.SendTextAsync(email);
        }
    }
}
