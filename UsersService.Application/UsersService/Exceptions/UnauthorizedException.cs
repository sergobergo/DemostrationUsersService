namespace UsersService.Application.UsersService.Exceptions
{
    public class UnauthorizedException : AppException
    {
        public UnauthorizedException(string message)
        : base(message) { }
        public UnauthorizedException(string message, Exception inner)
        : base(message, inner) { }
    }
}
