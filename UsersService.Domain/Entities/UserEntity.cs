
namespace UsersService.Domain.Entities
{
    public class UserEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? RefreshToken {  get; set; }
        public Guid? Jti {  get; set; }
        public DateTime? RefreshTokenExpiryTime {  get; set; }
    }
}
