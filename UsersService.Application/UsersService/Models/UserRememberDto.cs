namespace UsersService.Application.UsersService.Models
{
    public class UserRememberDto
    {
        public string Login {  get; set; }
        public string Code { get; set; }
        public UserRememberDto(string login, string code)
        {
            Login = login;
            Code = code;
        }
    }
}
