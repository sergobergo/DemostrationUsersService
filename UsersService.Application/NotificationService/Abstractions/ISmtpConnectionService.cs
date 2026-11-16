using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsersService.Application.NotificationService.Abstractions
{
    public interface ISmtpConnectionService
    {
        Task CheckConnectionAsync();
    }
}
