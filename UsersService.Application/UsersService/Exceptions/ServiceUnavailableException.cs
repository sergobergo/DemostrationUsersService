namespace UsersService.Application.UsersService.Exceptions
{
    public class ServiceUnavailableException : AppException
    {
        public ServiceUnavailableException(string message)
        : base(message) { }
        public ServiceUnavailableException(string message, Exception inner)
        : base(message, inner) { }
    }
}
