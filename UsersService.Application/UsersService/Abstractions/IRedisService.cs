namespace UsersService.Application.UsersService.Abstractions
{
    public interface IRedisService
    {
        Task<T?> GetDataAsync<T>(string key);
        Task SetAuthCodeAsync<T>(string key, T data);
        Task SetJtiAsync<T>(string key, T data);
        Task RemoveAsync(string key);
    }
}
