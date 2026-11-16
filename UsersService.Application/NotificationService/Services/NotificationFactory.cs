using Microsoft.Extensions.DependencyInjection;
using UsersService.Application.NotificationService.Abstractions;
using UsersService.Application.NotificationService.Enums;

namespace UsersService.Application.NotificationService.Services
{
    public class NotificationFactory : INotificationFactory
    {
        private readonly IServiceProvider _serviceProvider;
        public NotificationFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        public INotificationService<T> GetService<T>(NotificationServiceTypes service) where T : class 
        {
              switch (service)
            {
                case NotificationServiceTypes.EmailService :
                    return _serviceProvider.GetRequiredService<INotificationService<T>>();
                default :
                    throw new ArgumentException($"Unknown repository type: {service}");
            }
        }
    }
}
