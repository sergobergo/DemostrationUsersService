namespace UsersService.Application.UsersService.Models
{
    public class UserRegDto 
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Code { get; set; } = null;

        public UserRegDto()
        {
            this.Name = Name;
            this.Email = Email;
            this.Login = Login;
            this.Password = Password;
            this.Code = Code;
        }
        public UserRegDto(UserVerificationDto user)
        {
            this.Name = user.Name;
            this.Email = user.Email;
            this.Login = user.Login;
            this.Password = user.Password;
        }
    }
    
}
