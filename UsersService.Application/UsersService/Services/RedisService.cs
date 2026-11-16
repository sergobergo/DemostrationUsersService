using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using UsersService.Application.UsersService.Abstractions;
using UsersService.Application.UsersService.Exceptions;

namespace UsersService.Application.UsersService.Services
{
    public class RedisService : IRedisService
        {
            private readonly IDistributedCache _cache;
            private readonly ILogger<RedisService> _logger;
            public RedisService(
                IDistributedCache cache,
                ILogger<RedisService> logger)
            {
                _cache = cache ?? throw new ArgumentNullException(nameof(cache));
                _logger = logger;
            }

            /// <summary>
            /// Возвращает данные типа T? по ключу key
            /// Если сервис не доступен выбрасывает ошибку(ServiceUnavailableException)
            /// </summary
            public async Task<T?> GetDataAsync<T>(string key)
            {
                try
                {
                    var data = await _cache.GetStringAsync(key);
                    if (String.IsNullOrEmpty(data))
                        return default;
                    return JsonSerializer.Deserialize<T>(data);
                }
                catch(Exception ex)
                {
                    _logger.LogError(ex,"Redis service is not working.");
                    throw new ServiceUnavailableException("Redis service is not working.", ex);
                }
            }
            /// <summary>
            /// Записывает в Reddis данные объекта T по ключу Key с настройками Reddis'a для храненя кода аутентификации
            /// Если сервис не доступен выбрасывает ошибку(ServiceUnavailableException)
            /// </summary
            public async Task SetAuthCodeAsync<T>(string key, T data)
            {
                try
                {
                    if (key.Length == 0) throw new ArgumentException("Key is empty!");
                    var options = new DistributedCacheEntryOptions()
                    {
                        SlidingExpiration = TimeSpan.FromMinutes(5),
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                    };
                    await _cache.SetStringAsync(key, JsonSerializer.Serialize(data), options);
                    _logger.LogInformation("Data written successfully in Redis");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Redis service is not working.");
                    throw new ServiceUnavailableException("Redis service is not working.", ex);
                }
            }
            /// <summary>
            /// Записывает в Reddis данные объекта T по ключу Key с настройками Reddis'a для храненя кода JWT Token Identifier
            /// Если сервис не доступен выбрасывает ошибку(ServiceUnavailableException)
            /// </summary
            public async Task SetJtiAsync<T>(string key, T data)
            {
                try
                {
                    if (key.Length == 0) throw new ArgumentException("Key is empty!");
                    var options = new DistributedCacheEntryOptions()
                    {
                        SlidingExpiration = TimeSpan.FromMinutes(15)
                    };
                    await _cache.SetStringAsync(key, JsonSerializer.Serialize(data), options);
                    _logger.LogInformation("Data written successfully in Redis");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Redis service is not working.");
                    throw new ServiceUnavailableException("Redis service is not working.", ex);
                }
            }
            /// <summary>
            /// Удаляет запись из Reddis'a по ключу key
            /// </summary
            public async Task RemoveAsync(string key)
            {
                try
                {
                   await _cache.RemoveAsync(key);
                   _logger.LogInformation($"User succeful deleted from Redis.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Redis service is not working.");
                    throw new ServiceUnavailableException("Redis service is not working.", ex);
                }
            }
        }
}