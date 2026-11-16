using UsersService.Application.NotificationService.Models;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;

namespace UsersService.Application.NotificationService.Features.Mappers
{
    public static class NotificationDtoMapper
    {
        public static EmailDto ToEmailDto(this NotificationDto dto)
        {
            return new EmailDto
            {
                Sender = dto.Sender,
                Recipients = dto.Recipients,
                Subject = dto.Subject,
                Text = dto.Text
            };
        }
        public static NotificationDto ToNotificationDto(this EmailDto dto)
        {
            return new NotificationDto
            {
                Sender = dto.Sender,
                Recipients = dto.Recipients,
                Subject = dto.Subject,
                Text = dto.Text
            };
        }
    }
}
