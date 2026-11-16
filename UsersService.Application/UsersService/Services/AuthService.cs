using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using UsersService.Application.UsersService.Abstractions;
using UsersService.Application.UsersService.Exceptions;
using UsersService.Application.UsersService.Features.Mappers;
using UsersService.Application.UsersService.Models;
using UsersService.Application.UsersService.Models.Settings;
using UsersService.Domain;
using UsersService.Domain.Entities;

namespace UsersService.Application.UsersService.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserServiceDbContext _context;
        private readonly IRedisService _redis;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<AuthService> _logger;

        public AuthService
        (
            UserServiceDbContext context,
            IRedisService redis,
            IOptions<JwtSettings> options,
            ILogger<AuthService> logger

        )
        {
            _logger = logger;
            _redis = redis;
            _context = context;
            _jwtSettings = options.Value;
        }
        public async Task<UserLogDto> AddUserAsync(UserRegDto newUser)
        {
            if (await _context.Users.AnyAsync(u => 
            u.Login == newUser.Login || u.Email == newUser.Email))
            {
                throw new UserAlreadyExistException(
                    "User with this login or email already exists.");
            }
            try
            {
                await _context.Users.AddAsync(newUser.ToUserEntity());
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex,"Cannot save user.");
                throw new DbSaveChangesException("Cannot save user.", ex);
            }
            UserLogDto Reg = newUser.ToLogDto();
            _logger.LogInformation("User succesful added and saved.");
            return Reg;
        }

        //VerifyHashedPassword позволяет также настроить свои парметры верификации пользователя
        public async Task<TokenResponseDto> LoginAsync(UserLogDto user)
        {
            if (await _context.Users.AnyAsync(u => u.Login == user.Login))
            {
                var VerifyUser = await _context.Users.FirstAsync(u => u.Login == user.Login);
                if (new PasswordHasher<UserLogDto>().VerifyHashedPassword(user, VerifyUser.PasswordHash, user.Password)
                    == PasswordVerificationResult.Failed)
                {
                    throw new WrongPasswordException("Verification failed.");
                }
                return await CreateTokenResponse(VerifyUser);
            }
            throw new UserNotFoundException($"User {user.Login} not found");    

        }

        private async Task<TokenResponseDto> CreateTokenResponse(UserEntity VerifyUser)
        {
            return new TokenResponseDto
            {
                AccessToken = await CreateToken(VerifyUser),
                RefreshToken = await GenerateAndSaveRefreshTokenAsync(VerifyUser)
            };
        }

        public async Task<TokenResponseDto> RefreshTokenAsync(RefreshTokenRequestDto request)
        {
            var user = await ValidateRefreshTokenAsync(request.UserId, request.RefreshToken);
            return await CreateTokenResponse(user);
        }
        private async Task<UserEntity> ValidateRefreshTokenAsync(Guid UserId, string RefreshToken)
        {
            try
            {
                var user = await _context.Users.FindAsync(UserId);
                if (user == null || user.RefreshToken != RefreshToken 
                    || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                {
                    _logger.LogWarning("Error validation refresh token. User: {user}", UserId);
                    throw new ValidateRefreshTokenException("Error validation refresh token");
                }
                return user;
            }
            catch(Exception ex) when (ex is not ValidateRefreshTokenException)
            {
                _logger.LogError(ex, "Cannot find user.");
                throw new DbSaveChangesException("Cannot find user.", ex);
            }
        }
        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private async Task<string> GenerateAndSaveRefreshTokenAsync(UserEntity user)
        {
            var refreshToken = GenerateRefreshToken();
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error save refresh token.");
                throw new DbSaveChangesException("Error save refresh token.", ex);
            }
            return refreshToken;
        }
        private async Task<string> CreateToken(UserEntity user)
        {

            if (await _redis.GetDataAsync<string>(user.Id.ToString()) is not null)
            {
                _logger.LogInformation($"Redis have old Jti");
                await _redis.RemoveAsync(user.Id.ToString());
                _logger.LogInformation($"Old Jti removed from redis");
            }
            user.Jti = Guid.NewGuid();
            _logger.LogInformation($"Jti for {user.Login} created");
            _context.Update(user);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"New Jti saved in Db");
            await _redis.SetJtiAsync(user.Id.ToString(), user.Jti.ToString());
            _logger.LogInformation($"New Jti saved in Redis");

            var claims = new List<Claim>
            {
                 new Claim(ClaimTypes.Name, user.Name),
                 new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                 new Claim(ClaimTypes.Role, user.Role),
                 new Claim(JwtRegisteredClaimNames.Jti, user.Jti.ToString())
            };
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512);

            var tokenDescriptor = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires:DateTime.UtcNow.AddDays(1),
                signingCredentials: creds
                );
            _logger.LogInformation("JWT succesful created");
            return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        }

    }
}
 