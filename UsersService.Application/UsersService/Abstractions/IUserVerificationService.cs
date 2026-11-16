using UsersService.Application.UsersService.Models;

namespace UsersService.Application.UsersService.Abstractions
{
    public interface IUserVerificationService
    {
        Task RememberUserAndSendCodeAsync(UserRegDto user, CancellationToken ct);
        Task<TokenResponseDto?> UserCheckAndLoginAsync(string login, string code);
        Task RefreshAndSendCodeAsync(string login, CancellationToken ct);
    }
}
