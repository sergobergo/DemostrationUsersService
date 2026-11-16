using FluentValidation;
using MailKit.Net.Smtp;
using MassTransit;
using RabbitMQ.Client;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using UserService.API.Behaviors;
using UserService.API.Middleware;
using UsersService.Application.Services;
using UsersService.Domain;
using UsersService.Application.NotificationService.Abstractions;
using UsersService.Application.NotificationService.Services;
using UsersService.Application.NotificationService.Features.Validation.EmailValidator;
using UsersService.Application.NotificationService.Models.ServicesDto.EmailService;
using UsersService.Application.UsersService.Abstractions;
using UsersService.Application.UsersService.Services;
using UsersService.Application.UsersService.Models.Settings;
using UsersService.Application.UsersService.Features.Validation;
using UsersService.Application.NotificationService.Services.Consumers;

namespace UserService.API.Extensions
{
    public static class UserServiceCollectionExtensions
    {
        public static WebApplicationBuilder AddApplicationServices( this WebApplicationBuilder builder)
        {
            builder.Services.AddScoped<INotificationService<EmailDto>, EmailService<EmailDto>>();
            builder.Services.AddScoped<IJtiCheckService, JtiCheckService>();
            builder.Services.AddScoped<INotificationFactory, NotificationFactory>();
            builder.Services.AddScoped<IValidator<EmailContact>, EmailContactValidator>();
            builder.Services.AddScoped<IValidator<EmailDto>, EmailDtoValidator>();
            builder.Services.AddHttpClient<IPwnedPasswordService, PwnedPasswordService>();
            builder.Services.AddValidatorsFromAssembly(typeof(UserRegValidator).Assembly);
            builder.Services.AddScoped<IRedisService, RedisService>();
            builder.Services.AddScoped<IUserVerificationService, UserVerificationService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddSingleton<ISmtpClient, SmtpClient>();
            builder.Services.AddSingleton<SmtpConnectionService>();
            builder.Services.AddSingleton<ISmtpConnectionService>(
                sp => sp.GetRequiredService<SmtpConnectionService>());
            builder.Services.AddHostedService(
                sp => sp.GetRequiredService<SmtpConnectionService>());
            builder.Services.AddScoped<IMessageExchanger,MessageExchangerService>();
            return builder;
        }
        public static WebApplicationBuilder AddSettings(this WebApplicationBuilder builder)
        {
            builder.Services.Configure<NotificationSettings>(
                builder.Configuration.GetSection("NotificationSettings"));
            builder.Services.Configure<JwtSettings>(
                builder.Configuration.GetSection("JwtSettings"));
            builder.Services.Configure<RabbitMqSettings>(
                builder.Configuration.GetSection("RabbitMq"));
            return builder;
        }

        public static WebApplicationBuilder AddCustomMiddlewares(this WebApplicationBuilder builder)
        {
            builder.Services.AddTransient<GlobalExceptionHandler>();
            return builder;
        }
        public static WebApplicationBuilder AddRedisCache(this WebApplicationBuilder builder)
        {
            builder.Services.AddStackExchangeRedisCache(redisOptions =>
            {
                redisOptions.Configuration = builder.Configuration
                                                .GetConnectionString("Redis");
                redisOptions.InstanceName = "Users_";

            });
            return builder;
        }
        public static WebApplicationBuilder AddMassTransitWithRabbitMq(this WebApplicationBuilder builder)
        {
            builder.Services.AddMassTransit(x =>
            {
                x.AddConsumer<EmailConsumer>();

                x.UsingRabbitMq((ctx, cfg) =>
                {
                    var settings = ctx.GetRequiredService<IOptions<RabbitMqSettings>>().Value;
                    cfg.Host(new Uri($"amqp://{settings.Host}:{settings.Port}/{settings.VirtualHost}"), h =>
                    {
                        h.Username(settings.Username);
                        h.Password(settings.Password);
                    });
                    
                    cfg.AutoStart = true;

                    cfg.Send<EmailDto>(x =>
                    {
                        x.UseRoutingKeyFormatter(ctx => "email");
                    });
                    cfg.Message<EmailDto>(x =>
                    {
                        x.SetEntityName("notifications-exchange");
                    });

                    cfg.ReceiveEndpoint("email-queue", e =>
                    {
                        e.ConfigureConsumeTopology = false;

                        e.Bind("notifications-exchange", b =>
                        {
                            b.ExchangeType = ExchangeType.Direct;
                            b.RoutingKey = "email";
                        });

                        e.ConfigureConsumer<EmailConsumer>(ctx);
                    });

                });

            });
            return builder;
        }
        public static WebApplicationBuilder AddLoggerService(this WebApplicationBuilder builder)
        {
            builder.Services.AddScoped(
                    typeof(IPipelineBehavior<,>),
                    typeof(LoggingPipelineBehavior<,>));
            return builder;
        }

        public static WebApplicationBuilder AddMediatRService(this WebApplicationBuilder builder)
        {
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
            return builder;
        }
        public static WebApplicationBuilder AddSerilogServices(this WebApplicationBuilder builder)
        {
            builder.Host.UseSerilog((context, config) =>
            {
                config
                    // Читаем настройки из appsettings.json
                    .ReadFrom.Configuration(context.Configuration)
                    // Обогащаем логи контекстом
                    .Enrich.FromLogContext()                     // Добавляет пользовательские свойства
                    .Enrich.WithMachineName()                    // Имя машины
                    .Enrich.WithEnvironmentUserName()            // Имя пользователя
                    .Enrich.WithThreadId()                       // ID потока
                    .Enrich.WithProcessId()                      // ID процесса
                    .Enrich.WithProperty("ServiceName", "UsersService")

                    // Куда пишем (Sinks)
                    .WriteTo.Console(outputTemplate:
                        "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File(
                        path: "logs/log-.txt",
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ServiceName}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")

                    // Опционально: минимум логирования
                    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning);
            });
            return builder;
        }
        public static WebApplicationBuilder AddData(this WebApplicationBuilder builder)
        {
            builder.Services.AddDbContext<UserServiceDbContext>(
                options =>
                {
                    options.UseNpgsql(builder.Configuration.GetConnectionString(nameof(UserServiceDbContext)));
                });
            return builder;
        }
        public static WebApplicationBuilder AddSwagger(this WebApplicationBuilder builder)
        {
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "",
                    Version = "v1",
                    Description = "",
                });
                options.AddSecurityDefinition(name: JwtBearerDefaults.AuthenticationScheme,
                    securityScheme: new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Description = "Enter 'Bearer' following by space and JWT.",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = "Bearer"
                    });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = JwtBearerDefaults.AuthenticationScheme
                            }
                        }, new string[]{}
                    }
                });
            });
            return builder;
        }
        public static WebApplicationBuilder AddCorsService(this WebApplicationBuilder builder)
        {
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend",
                    policy =>
                    {
                        policy.WithOrigins(
                            "https://localhost:7063"//Swagger
                        )
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials(); // если передаёте куки / авторизацию
                    });
            });
            return builder;
        }
        public static WebApplicationBuilder AddTokenReader(this WebApplicationBuilder builder)
        {
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(opt =>
                {
                    opt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["JwtSettings:Audience"],
                        ValidateLifetime = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Key"]!)),
                        ValidateIssuerSigningKey = true
                    };
                });
            return builder;
        }
    }
}
