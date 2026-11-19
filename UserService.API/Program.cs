using Microsoft.EntityFrameworkCore;
using UserService.API.Extensions;
using UserService.API.Middleware;
using UsersService.Application.UsersService.Models.Settings;
using UsersService.Domain;

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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<UserServiceDbContext>();

    if (dbContext.Database.IsRelational())
    {
        dbContext.Database.Migrate();
    }
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.UseMiddleware<JtiCheckMiddleware>();

app.MapControllers();

app.Run();

public partial class Program { }