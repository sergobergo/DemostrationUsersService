using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UsersService.Application.NotificationService.Enums;

namespace UsersService.Application.NotificationService.Abstractions
{
    public interface INotificationFactory
    {
        INotificationService<T> GetService<T>(NotificationServiceTypes service) where T : class;
    }
}
