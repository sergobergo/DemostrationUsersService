namespace UsersService.Application.UsersService.Exceptions
{
    public class DbSaveChangesException : AppException
    {
        public DbSaveChangesException(string message)
        : base(message) { }
        public DbSaveChangesException(string message, Exception inner)
        : base(message, inner) { }
    }
}
