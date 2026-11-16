namespace UsersService.Application.UsersService.Abstractions
{
    public interface IPwnedPasswordService
    {
        Task<bool> IsPasswordPwned(string password, CancellationToken ct);
    
    }
}
