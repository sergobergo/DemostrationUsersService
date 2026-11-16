namespace UsersService.Application.UsersService.Exceptions
{
    public class WrongPasswordException : AppException
    {
        public WrongPasswordException(string message)
        : base(message) { }
        public WrongPasswordException(string message, Exception inner)
        : base(message, inner) { }
    }
}
