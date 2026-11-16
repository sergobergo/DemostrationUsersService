using Microsoft.AspNetCore.Identity;
using UsersService.Application.NotificationService.Models;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.UsersService.Models;
using UsersService.Domain.Entities;

namespace UsersService.Application.UsersService.Features.Mappers
{
    public static class UserMapper
    {
        public static UserLogDto ToLogDto(this UserRegDto regDto)
        {
            return new UserLogDto
            {
                Login = regDto.Login,
                Password = regDto.Password
            };
        }
        public static UserEntity ToUserEntity(this UserRegDto Dto)
        {
            return new UserEntity
            {
                Name = Dto.Name,
                Email = Dto.Email,
                Login = Dto.Login,
                PasswordHash = new PasswordHasher<UserRegDto>()
                .HashPassword(Dto, Dto.Password),
                Role = "User",
                RefreshToken = "",
                RefreshTokenExpiryTime = null
            };
        }
        public static NotificationDto ToNotificationDto(this UserRegDto dto)
        {
            var contact = new EmailContact() { Name = dto.Name, EmailAddress = dto.Email };
            var newDto = new NotificationDto();
            newDto.Recipients.Add(contact);
            if(dto.Code is not null)
            {
                newDto.Text = dto.Code;
            }
            return newDto;
        }
    }
}
