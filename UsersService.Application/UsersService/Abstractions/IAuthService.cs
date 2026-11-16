using UsersService.Application.UsersService.Models;


namespace UsersService.Application.UsersService.Abstractions
{
    public interface IAuthService
    {
        Task<UserLogDto> AddUserAsync(UserRegDto newUser);
        Task<TokenResponseDto> LoginAsync(UserLogDto user);
        Task<TokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request);

    }
}
