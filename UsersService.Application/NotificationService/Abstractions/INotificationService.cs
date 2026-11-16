using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsersService.Application.NotificationService.Models;

namespace UsersService.Application.NotificationService.Abstractions
{
    public interface INotificationService<T> where T : class
    {
        IValidator<T> GetValidator();
        T CreateDto();
        T CreateDto(NotificationDto dto);
        Task SendTextAsync(T dto);
    }
}
