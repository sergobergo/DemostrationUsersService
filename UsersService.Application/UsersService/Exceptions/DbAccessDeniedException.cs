namespace UsersService.Application.UsersService.Exceptions
{
    public class DbAccessDeniedException : AppException
    {
        public DbAccessDeniedException(string message)
        : base(message) { }
        public DbAccessDeniedException(string message, Exception inner)
        : base(message, inner) { }
    }
}
