namespace UsersService.Application.UsersService.Exceptions
{
    public class ValidateRefreshTokenException : AppException
    {
        public ValidateRefreshTokenException(string message)
        : base(message) { }
        public ValidateRefreshTokenException(string message, Exception inner)
        : base(message, inner) { }
    }
}
