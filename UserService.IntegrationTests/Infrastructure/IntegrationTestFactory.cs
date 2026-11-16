using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using UserService.IntegrationTests.Infrastructure.Settings;
using UsersService.Application.NotificationService.Abstractions;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.NotificationService.Services.Consumers;
using UsersService.Application.UsersService.Models.Settings;
using UsersService.Domain;

namespace UserService.IntegrationTests.Infrastructure
{
    public class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        public readonly PostgreDbTestSettings _PostgreDbSettings;
        public readonly RabbitMqTestSettings _RabbitMqSettings;
        public readonly RedisTestSettings _RedisSettings;
        private readonly PostgreSqlContainer _postgres;
        private readonly RabbitMqContainer _rabbitMq;
        private readonly RedisContainer _redis;
        private readonly IContainer _mailPit;

        public IntegrationTestFactory()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.Testing.json", optional: false)
                .Build();

            _PostgreDbSettings = configuration
                .GetSection("PostgreDb")
                .Get<PostgreDbTestSettings>()!;

            _RabbitMqSettings = configuration
                .GetSection("RabbitMq")
                .Get<RabbitMqTestSettings>()!;

            _RedisSettings = configuration
                .GetSection("Redis")
                .Get<RedisTestSettings>()!;

            _mailPit = new ContainerBuilder("axllent/mailpit")
                .WithPortBinding(1025, true)
                .WithPortBinding(8025, true)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilInternalTcpPortIsAvailable(8025))
                .Build();

            _postgres = new PostgreSqlBuilder("postgres:latest")
               .WithDatabase(_PostgreDbSettings.Database)
               .WithUsername(_PostgreDbSettings.Username)
               .WithPassword(_PostgreDbSettings.Password)
               .Build();

            _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management")
                .WithUsername(_RabbitMqSettings.Username)
                .WithPassword(_RabbitMqSettings.Password)
                .Build();

            _redis = new RedisBuilder("redis:latest")
                .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilInternalTcpPortIsAvailable(_RedisSettings.Port))
                .Build();
        }
        public string MailPitApiUrl =>
            $"http://localhost:{_mailPit.GetMappedPublicPort(8025)}";

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                 _postgres.StartAsync(),
                 _rabbitMq.StartAsync(),
                 _redis.StartAsync(),
                 _mailPit.StartAsync());

            using var scope = Services.CreateScope();
            var dbContext = scope.ServiceProvider
                .GetRequiredService<UserServiceDbContext>();
            await dbContext.Database.MigrateAsync();

        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            await _postgres.DisposeAsync();
            await _rabbitMq.DisposeAsync();
            await _redis.DisposeAsync();
            await _mailPit.DisposeAsync();
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                
                services.RemoveAll<DbContextOptions<UserServiceDbContext>>();
                services.AddDbContext<UserServiceDbContext>(options =>
                    options.UseNpgsql(_postgres.GetConnectionString()));
                
                services.RemoveAll<IDistributedCache>();
                services.RemoveAll<IConnectionMultiplexer>();

                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = _redis.GetConnectionString();
                    options.InstanceName = "Users_";
                });
                
                var massTransitServices = services
                   .Where(s => s.ServiceType.FullName?.Contains("MassTransit") == true
                            || s.ImplementationType?.FullName?.Contains("MassTransit") == true)
                   .ToList();
                foreach (var service in massTransitServices)
                    services.Remove(service);

                var healthChecks = services
                     .Where(s =>
                         s.ServiceType.FullName?.Contains("HealthCheck") == true ||
                         s.ImplementationType?.FullName?.Contains("HealthCheck") == true)
                     .ToList();
                foreach (var service in healthChecks)
                services.Remove(service);

                services.RemoveAll<IBusControl>();
                services.RemoveAll<IBus>();
                services.RemoveAll<IPublishEndpoint>();
                services.RemoveAll<ISendEndpointProvider>();

                services.RemoveAll<IMessageExchanger>();
                services.AddScoped<IMessageExchanger, DirectMessageExchanger>();

                services.PostConfigure<NotificationSettings>(options =>
                {
                    options.Host = "localhost";
                    options.Port = _mailPit.GetMappedPublicPort(1025);
                    options.Name = "TestSender";
                    options.EmailAddress = "test@test.com";
                    options.EmailPassword = "";
                });
            });
        }
    }
}
