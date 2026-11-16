using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using UsersService.Application.UsersService.Abstractions;

namespace UserService.API.Middleware
{
    public class JtiCheckMiddleware 
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<JtiCheckMiddleware> _logger;

        public JtiCheckMiddleware(ILogger<JtiCheckMiddleware> logger, RequestDelegate next)
        {
            _logger = logger;
            _next = next;
        }
        public async Task InvokeAsync(HttpContext context, IJtiCheckService _jtiCheckService)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                await _next(context);
                return;
            }

            var user = context.User;

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            _logger.LogInformation($"Id from token: {userId}");

            var jti = user.FindFirstValue(JwtRegisteredClaimNames.Jti);
            _logger.LogInformation($"Jti from token: {jti}");

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(jti))
            {
                _logger.LogWarning("Missing userId or jti in token");
                await WriteUnauthorizedAsync(context, "Invalid token claims");
                return;
            }

            var isValid = await _jtiCheckService.JtiCheck(userId, jti);

            if (!isValid)
            {
                _logger.LogWarning("Token revoked for user {UserId}, jti {Jti}", userId, jti);
                await WriteUnauthorizedAsync(context, "Token has been revoked");
                return;
            }

            await _next(context);
        }
        private static async Task WriteUnauthorizedAsync(HttpContext context, string message)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message });
        }
    }
}
