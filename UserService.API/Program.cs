using UserService.API.Extensions;
using UserService.API.Middleware;
using UsersService.Application.UsersService.Models.Settings;

var builder = WebApplication.CreateBuilder(args);

var settings = builder.Configuration
    .GetSection("NotificationSettings")
    .Get<NotificationSettings>();

Console.WriteLine($"Host: '{settings?.Host}'");
Console.WriteLine($"Port: '{settings?.Port}'");
Console.WriteLine($"Email: '{settings?.EmailAddress}'");

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();


builder
    .AddRedisCache()
    .AddData()
    .AddSwagger()
    .AddMassTransitWithRabbitMq()
    .AddApplicationServices()
    .AddTokenReader()
    .AddMediatRService()
    .AddLoggerService()
    .AddSerilogServices()
    .AddCorsService()
    .AddSettings()
    .AddCustomMiddlewares();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandler>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseMiddleware<JtiCheckMiddleware>();

app.MapControllers();

app.Run();

public partial class Program { }