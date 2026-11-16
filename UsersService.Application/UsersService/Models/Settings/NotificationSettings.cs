using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UsersService.Application.UsersService.Models.Settings
{
    public class NotificationSettings
    {
        public string Name { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public string EmailPassword { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }

    }
}
