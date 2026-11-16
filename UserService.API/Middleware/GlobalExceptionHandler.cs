using UsersService.Application.UsersService.Exceptions;
namespace UserService.API.Middleware
{
    public class GlobalExceptionHandler : IMiddleware
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (UserAlreadyExistException ex)
            {
                _logger.LogWarning(ex, "User already exists");
                await WriteResponseAsync(context, StatusCodes.Status409Conflict, ex.Message);
            }
            catch (DbSaveChangesException ex)
            {
                _logger.LogError(ex, "Service Unavailable");
                await WriteResponseAsync(context, StatusCodes.Status503ServiceUnavailable, "Service temporarily unavailable");
            }
            catch (ServiceUnavailableException ex)
            {
                _logger.LogError(ex, "Service Unavailable");
                await WriteResponseAsync(context, StatusCodes.Status503ServiceUnavailable, "Service temporarily unavailable");
            }
            catch (UserNotFoundException ex)
            {
                _logger.LogWarning(ex, "User not found");
                await WriteResponseAsync(context, StatusCodes.Status404NotFound, ex.Message);
            }
            catch (ValidateRefreshTokenException ex)
            {
                _logger.LogWarning(ex, "Refresh token validation error");
                await WriteResponseAsync(context, StatusCodes.Status401Unauthorized, ex.Message);
            }
            catch (WrongPasswordException ex)
            {
                _logger.LogWarning(ex, "Wrong password");
                await WriteResponseAsync(context, StatusCodes.Status400BadRequest, ex.Message);
            }
            catch (DbAccessDeniedException ex)
            {
                _logger.LogError(ex, "Service Unavailable");
                await WriteResponseAsync(context, StatusCodes.Status503ServiceUnavailable, "Service temporarily unavailable");
            }
            catch (UnauthorizedException ex)
            {
                _logger.LogWarning(ex, "Unauthorized");
                await WriteResponseAsync(context, StatusCodes.Status401Unauthorized, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                await WriteResponseAsync(context, StatusCodes.Status500InternalServerError, "Internal server error");
            }
        }
            private async Task WriteResponseAsync(HttpContext context, int statusCode, string message)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogWarning(
                    "Response already started (status {StatusCode}), cannot write error: {Message}",
                    context.Response.StatusCode, message);
                return;
            }
            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = message });
        }
    }
}
