namespace UsersService.Application.UsersService.Exceptions
{
    public class UserNotFoundException : AppException
    {
        public UserNotFoundException(string message)
        : base(message) { }
        public UserNotFoundException(string message, Exception inner)
        : base(message, inner) { }
    }
}
